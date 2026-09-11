using System.Linq;
using ChinaBettle.AI;
using ChinaBettle.Foundation.AI;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Trust;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 秦军 AI 大脑（真源 AI-01/02/05、SLICE-04）：情报模块 → 信任桶 → 目标优先级 → 行为决策 → 欺骗行为。
    ///
    /// 行为：
    /// ①决策门控（TRUST-05/07）：按 AI 自己采信到的载荷档位与信任桶联合门控；
    ///    存疑/冷桶 → 触发**存疑型强复核**（冻结高利害行动 30–60s）；
    /// ②欺骗行为（AI-05）：兵力劣势（ai &lt; 0.7×player）→ 增灶示强；评估可设伏 → 减灶示弱；
    ///    与玩家同规则（30 谋略点 / 90s CD），走同一痕迹与识破链路；
    /// ③军事态势（AI-02 目标优先级简化版）：采信"敌弱"→ 前压夺点；"敌强"或情报不足 → 依托野王—高都守势。
    ///
    /// ML-Agents 不进主链路（AI-04）。
    /// </summary>
    public sealed class AiBrain
    {
        private readonly BattleSimulation sim;

        public AiBrain(BattleSimulation sim)
        {
            this.sim = sim;
            Decision = new IntelDrivenDecision(sim.AiTrust, sim.Rules.Trust, sim.Rules.Credibility);
            Deception = new AiDeceptionPlanner(sim.AiPoints, sim.Rules.AiDeception);
        }

        /// <summary>AI 侧采信链路（情报 → 信任桶 → 门控）。</summary>
        public IntelDrivenDecision Decision { get; }

        /// <summary>AI 欺骗行为（AI-05）。</summary>
        public AiDeceptionPlanner Deception { get; }

        /// <summary>最近一次门控结果（无情报时为 null）。</summary>
        public DecisionGate? LastGate { get; private set; }

        public void Think(float now)
        {
            LastGate = EvaluateGate(now);

            // ① 强复核：存疑档 / 可信档撞桶 / 桶 <0.3 → 冻结全部高利害行动（TRUST-07）。
            if (LastGate == DecisionGate.ForceRecheck && !sim.AiIsFrozen(now))
            {
                sim.EnterAiStrongRecheck(now);
                return;
            }

            if (sim.AiIsFrozen(now))
            {
                return; // 冻结期内保持态势，不进攻、不撤退、不调兵
            }

            // ② 欺骗行为（AI-05）。
            TryDeceive(now);

            // ③ 军事态势。
            ApplyPosture(now);
        }

        private DecisionGate? EvaluateGate(float now)
        {
            float? aggregate = sim.AiIntel.AggregateCredibility(sim.AiTopic, now);
            if (aggregate is null)
            {
                return null;
            }

            return DecisionGateEvaluator.Evaluate(
                CredibilityCalculator.Tier(aggregate.Value, sim.Rules.Credibility),
                sim.AiTrust.Get(sim.AiTopic),
                sim.Rules.Trust,
                sim.Rules.Credibility);
        }

        private void TryDeceive(float now)
        {
            float aiStrength = sim.EffectiveStrength(Faction.Qin);
            float playerStrength = sim.EffectiveStrength(Faction.Zhao);

            // "评估可设伏诱敌"（AI-05②）：秦军已有单位占据减速地形（山林），且赵军逼近该单位 90 米内。
            bool canAmbush = sim.Units.Any(q => q.Alive && q.Faction == Faction.Qin &&
                                                sim.Map.TerrainAt(q.Position) == TerrainClass.Difficult &&
                                                sim.Units.Any(z => z.Alive && z.Faction == Faction.Zhao &&
                                                                   z.Position.DistanceTo(q.Position) <= 90f));

            // 欺骗话题 = 秦军自己兵力（玩家关心主题）；区域 = 秦军主力所在区域。
            var qinMain = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin).ToList();
            if (qinMain.Count == 0)
            {
                return;
            }

            var centroid = new MapPoint(qinMain.Average(u => u.Position.X), qinMain.Average(u => u.Position.Z));
            string areaId = sim.Map.AreaIdAt(centroid);

            var cast = Deception.TryCast(
                aiStrength, playerStrength, canAmbush,
                sim.PlayerTopic, areaId, trueTroopCount: aiStrength, nowSeconds: now);

            if (cast is not null)
            {
                sim.RegisterAiDeception(cast);
            }
        }

        private void ApplyPosture(float now)
        {
            var qinUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsRouted).ToList();
            if (qinUnits.Count == 0)
            {
                return;
            }

            // ── 剧本态势优先（CP-02）：守势 / 追击 / 合围由幕驱动；情报门控已在 ① ② 步决定"能不能动" ──
            // 设计意图：剧本负责"史实节拍"（何时佯退、何时合围），AI 情报链路负责"信不信、动不动"。
            // 若 AI 在守势幕就开局前压，①② 的侦查/桶预养节拍（TRUST-09）会被吃掉、赵军提前被打崩，
            // 七幕剧本失去意义——故这两幕必须严格据守势线待命。
            if (sim.AiPosture == AiPosture.Encircle)
            {
                ApplyEncirclePosture();
                return;
            }

            if (sim.AiPosture == AiPosture.Pursue)
            {
                ApplyPursuePosture();
                return;
            }

            if (sim.CurrentAct <= CampaignAct.Challenge)
            {
                ApplyHoldPosture();
                return;
            }

            // 目标优先级（真源 AI-02）：priority = 成功收益×成功率 − 失败代价×(1−成功率) − 不行动代价。
            // 这里把三个候选目标（夺赵军大帐 / 封锁粮道 / 固守）按公式排序，取代原先的二元 if。
            // "不行动代价"随围困时长上升——这正是 AI-02 要消除的死锁：粮道已断时，
            // 继续干等的代价会超过进攻代价，把封锁/夺点顶到队首。
            var zhaoUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao).ToList();
            var zhaoCentroid = zhaoUnits.Count > 0
                ? new MapPoint(zhaoUnits.Average(u => u.Position.X), zhaoUnits.Average(u => u.Position.Z))
                : sim.Map.BaseOf(Faction.Zhao);

            float believedPlayerStrength = sim.LastPayloadAiSaw;
            bool believesEnemyWeak = believedPlayerStrength >= 0f &&
                                    believedPlayerStrength < 0.75f * sim.EffectiveStrength(Faction.Qin) &&
                                    LastGate is DecisionGate.Act or DecisionGate.Confident;

            var enemyCamp = sim.Map.CampOf(Faction.Zhao)!;
            var holdLine = sim.Map.AiHoldLine; // 守势线（地图定义，真源 MAP/CP）

            // 情报门控决定"能不能信"：未达可决策档时，成功率下调、固守的相对价值上升。
            float intelConfidence = LastGate switch
            {
                DecisionGate.Confident => 0.9f,
                DecisionGate.Act => 0.75f,
                _ => 0.35f,
            };

            // 不行动代价：围困越久越高（AI-02 的反死锁项）。
            float siegePressure = sim.CurrentAct >= CampaignAct.Siege ? 20f : 0f;

            var goals = new[]
            {
                new Goal("夺赵军大帐", SuccessGain: 100f, SuccessRate: intelConfidence,
                         FailureCost: 60f, InactionCost: siegePressure * 0.5f),
                new Goal("封锁粮道", SuccessGain: 70f, SuccessRate: System.Math.Clamp(intelConfidence + 0.15f, 0f, 1f),
                         FailureCost: 20f, InactionCost: siegePressure),
                new Goal("固守待机", SuccessGain: 40f, SuccessRate: 0.7f,
                         FailureCost: 10f, InactionCost: siegePressure * 0.25f),
            };

            var chosen = new GoalPrioritizer().Top(goals);
            MapPoint objective = chosen?.Key == "夺赵军大帐" && believesEnemyWeak ? enemyCamp.Position : holdLine;

            sim.LogAi(
                $"白起目标优先级（AI-02）：选定「{chosen?.Key}」（{string.Join(" / ", goals.Select(g => $"{g.Key}={g.Priority:0}"))}）；" +
                $"敌弱判定={believesEnemyWeak}，情报档={LastGate?.ToString() ?? "无情报"}");

            foreach (var unit in qinUnits)
            {
                if (unit.IsScout)
                {
                    // 斥候前出盯住赵军主力（SLICE-03①：只有斥候目视才产情报）。
                    unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                        zhaoCentroid.X + (unit.Position.X < 0 ? -25f : 25f),
                        zhaoCentroid.Z + 40f));
                    continue;
                }

                // 守势时贴守势线；攻势时前压敌营。弩兵留在后排（射程 180m）。
                float standoff = unit.Definition.HasRangedAttack ? 90f : 5f;
                var goal = unit.Definition.HasRangedAttack
                    ? new MapPoint(objective.X, objective.Z + (believesEnemyWeak ? standoff : standoff))
                    : new MapPoint(objective.X + (unit.Position.X < 0 ? -12f : 12f), objective.Z);

                unit.MoveGoal = sim.Map.ClampToBounds(goal);
            }

            sim.LogAi(believesEnemyWeak
                ? $"秦军采信当前载荷（{believedPlayerStrength:0} 人）判定赵军势弱 → 前压夺赵军大帐（门控 {LastGate}）"
                : $"秦军依托守势线固守（门控 {LastGate?.ToString() ?? "无情报"}）");
        }

        /// <summary>
        /// 追击态势（CP-02 ④，白起佯败诱敌）：秦军**且战且退**，把赵军往丹水南岸引，
        /// 使其脱离壁垒——这是合围的前置动作（史实："秦军佯败而走，赵军悉众追之"）。
        ///
        /// 落地口径：主力徐徐向南退往渡口一线（保持与赵军接触但不决战），
        /// 两翼骑兵开始外张（为 ⑤ 幕包抄留位）；斥候继续盯赵军主力。
        /// </summary>
        private void ApplyPursuePosture()
        {
            var ford = FindFord();
            MapPoint axis = ford?.Center ?? sim.Map.AiHoldLine;

            var qinUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsRouted).ToList();
            var zhaoUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao).ToList();
            var zhaoCentroid = zhaoUnits.Count > 0
                ? new MapPoint(zhaoUnits.Average(u => u.Position.X), zhaoUnits.Average(u => u.Position.Z))
                : sim.Map.BaseOf(Faction.Zhao);

            foreach (var unit in qinUnits)
            {
                if (unit.IsScout)
                {
                    float standoff = sim.Rules.ScoutSightRadius * 0.6f;
                    unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                        zhaoCentroid.X + (unit.Position.X < 0 ? -standoff : standoff),
                        zhaoCentroid.Z - standoff));
                    continue;
                }

                if (unit.IsCavalry)
                {
                    // 两翼骑兵外张，为合围预留包抄位（CP-02 ⑤ 的伏笔）。
                    float wing = unit.Position.X < axis.X ? -70f : 70f;
                    unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(axis.X + wing, axis.Z + 40f));
                    continue;
                }

                // 步兵/弩兵：退往渡口南侧（且战且退，不与赵军决战）。
                // 距离纪律：退到敌方远程射程之外，但**必须有上限**——退过头会撞上地图边界
                // （ClampToBounds 夹住后单位卡死在边界，实测秦军目标变成 (…, 210) 即地图边缘）。
                // 故取"敌方射程 + 40m"与"地图南向余量的 60%"中的较小值。
                float enemyRangedReach = sim.Units
                    .Where(z => z.Alive && z.Faction == Faction.Zhao && z.Definition.HasRangedAttack)
                    .Select(z => z.Definition.RangedRange)
                    .DefaultIfEmpty(0f)
                    .Max();

                float southRoom = (sim.Map.MaxZ - axis.Z) * 0.6f;
                float disengage = System.MathF.Min(enemyRangedReach + 40f, southRoom);

                unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                    axis.X + (unit.Position.X - axis.X) * 0.5f,
                    axis.Z + disengage));
            }

            sim.LogAi($"秦军追击态势（CP-02 ④）：佯败退往渡口、两翼骑兵外张（门控 {LastGate?.ToString() ?? "无情报"}）");
        }

        /// <summary>丹水渡口（嵌在不可通行河道内的可通行小体块，MAP-11）。</summary>
        private TerrainVolume? FindFord() =>
            sim.Map.Volumes
                .Where(v => v.Terrain == TerrainClass.Passable)
                .Where(v => sim.Map.Volumes.Any(big => big.Terrain == TerrainClass.Impassable &&
                                                        System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                        System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .OrderBy(v => v.Width * v.Depth)
                .FirstOrDefault();

        /// <summary>
        /// 守势态势（CP-02 ①②）：秦军停留在守势线附近**不主动接战**。
        /// 斥候照常前出侦查（SLICE-03①：只有斥候目视才产情报，桶预养依赖它），其余单位贴守势线待命。
        /// </summary>
        private void ApplyHoldPosture()
        {
            var holdLine = sim.Map.AiHoldLine;
            var zhaoUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao).ToList();
            var zhaoCentroid = zhaoUnits.Count > 0
                ? new MapPoint(zhaoUnits.Average(u => u.Position.X), zhaoUnits.Average(u => u.Position.Z))
                : sim.Map.BaseOf(Faction.Zhao);

            var qinUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsRouted).ToList();
            int index = 0;
            foreach (var unit in qinUnits)
            {
                if (unit.IsScout)
                {
                    // 斥候前出侦查，但**停在敌方远程射程之外**：
                    // 斥候是侦查单位（不主动接战），若贴脸敌弩兵射程（180m）会被开局秒杀，
                    // 使 ① ② 幕的目视情报链（SLICE-03①）与桶预养（TRUST-09）整体失效。
                    float standoff = sim.Rules.ScoutSightRadius * 0.6f; // 在视野内、远离远程射程
                    unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                        zhaoCentroid.X + (unit.Position.X < 0 ? -standoff : standoff),
                        zhaoCentroid.Z + standoff));
                    continue;
                }

                unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                    holdLine.X + (index - qinUnits.Count / 2f) * 14f,
                    holdLine.Z));
                index++;
            }

            sim.LogAi($"秦军守势（CP-02 ①②）：据守势线待命、斥候前出侦查（门控 {LastGate?.ToString() ?? "无情报"}）");
        }

        /// <summary>
        /// 合围态势（CP-06）：抢占丹水渡口与赵军粮道走廊，就地封锁——不再强攻壁垒。
        /// 落点按地图语义取（渡口＝嵌在不可通行河道内的可通行体块），不硬编码坐标。
        /// </summary>
        private void ApplyEncirclePosture()
        {
            var ford = sim.Map.Volumes
                .Where(v => v.Terrain == TerrainClass.Passable)
                .Where(v => sim.Map.Volumes.Any(big => big.Terrain == TerrainClass.Impassable &&
                                                        System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                        System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .OrderBy(v => v.Width * v.Depth)
                .FirstOrDefault();

            if (ford is null)
            {
                return;
            }

            var qinUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsRouted).ToList();
            int index = 0;
            foreach (var unit in qinUnits)
            {
                if (unit.IsScout)
                {
                    continue;
                }

                // 落点纪律：**必须在可通行处**。渡口仅 14m 宽（MAP-11），横向展开会落到
                // 两侧不可通行河道上，单位被 Clamp 挡住后卡死/绕行，包围圈散开。
                // 故改为沿**渡口轴线南北分列**：少量守军堵在渡口，其余在南岸（秦侧）列阵封锁。
                bool atFord = index < 2; // 前两队扼守渡口本体
                float z = atFord ? ford.Center.Z : ford.Center.Z + 30f + (index - 2) * 10f;

                unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                    ford.Center.X + (atFord ? (index == 0 ? -3f : 3f) : (index % 2 == 0 ? -20f : 20f)),
                    z));
                index++;
            }

            sim.LogAi($"秦军合围态势（CP-06）：扼守丹水渡口并封锁粮道（渡口 {ford.Center}）");
        }
    }

}