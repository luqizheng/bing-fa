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

        /// <summary>强复核的冷却截止时刻（TRUST-07 重入保护：避免"复核→冻结→再复核"的永久瘫痪）。</summary>
        private float recheckCooldownUntilSeconds = -1f;

        /// <summary>AI-06 抽调态势的结束时刻（-1 表示当前无抽调态势）。</summary>
        private float divertUntilSeconds = -1f;

        /// <summary>决策时本阵守备队数（用于编年史对照，展示"空档"确实出现）。</summary>
        private int homeGarrisonAtDecision;

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
            // ── AI-06⑤：抽调窗口的关闭必须在最前面处理 ──
            // 若放在 TryRespondToDeceptionByDiverting 内部，会被"冻结提前 return"绕过，
            // 导致抽调态势永续、空档不消失（另一种失衡）。故提到此处。
            if (divertUntilSeconds >= 0f && now >= divertUntilSeconds)
            {
                divertUntilSeconds = -1f;
                sim.LogAi("秦军抽调态势结束——被抽走的方向开始补位填回（AI-06⑤ 窗口关闭）");
            }

            LastGate = EvaluateGate(now);

            if (sim.AiIsFrozen(now))
            {
                return; // 冻结期内保持态势，不进攻、不撤退、不调兵（TRUST-07）
            }

            // ① 强复核：存疑档 / 可信档撞桶 / 桶 <0.3 → 冻结全部高利害行动（TRUST-07）。
            //
            // **重入保护（v1.4 修正）**：真源要求"冻结 30–60 秒后用**新鲜情报**重算再决策"，
            // 而不是反复冻结。此前每轮决策都重新触发强复核（冻结期内从不重评门控，
            // LastGate 恒为 ForceRecheck），于是 AI **永久瘫痪**——实测连续 240 秒无法行动，
            // 既不能按剧本合围、也不能响应欺骗载荷。
            // 现在：同一主题的强复核在 `StrongRecheckCooldownSeconds` 内只触发一次，
            // 冷却期内按"无新情报可复核"处理——正常执行态势（保持战场推进，不僵死）。
            bool recheckCoolingDown = now < recheckCooldownUntilSeconds;
            if (LastGate == DecisionGate.ForceRecheck && !recheckCoolingDown)
            {
                sim.EnterAiStrongRecheck(now);
                recheckCooldownUntilSeconds = now + sim.Rules.StrongRecheckSeconds * 2f;
                return;
            }

            if (LastGate == DecisionGate.ForceRecheck && recheckCoolingDown)
            {
                // 冷却期内的存疑：不采信载荷，但**也不瘫痪**——按守势态势保持态势。
                ApplyHoldPosture();
                return;
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

            // ── AI-06：欺骗战果转化——若 AI 采信了"某方向敌弱/敌退"的载荷，必须**抽调**兵力过去 ──
            // 这是本次批次的核心：没有抽调，欺骗只改变站位、不产生战果，玩家就没有施计动机。
            // 抽调造成【载荷指向方向的相反方向】出现可观测、可打击、有时窗的兵力空档。
            if (TryRespondToDeceptionByDiverting(now))
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
        /// **AI-06 欺骗战果转化**：AI 采信了"某方向敌弱/敌退"的篡改载荷时，从**其它方向**抽调兵力前往，
        /// 从而在被抽走兵力的方向留下**可观测、可打击、有时窗**的空档。
        ///
        /// 为什么必须有这一步：此前 AI 只是把整条阵线平移（"前压"/"守势"），
        /// 骗与不骗的结果几乎一样——欺骗改变了站位却没改变**力量对比**，
        /// 玩家于是没有施计动机。抽调让"骗"直接转化为"局部兵力优势"。
        ///
        /// 返回 true 表示本次已按抽调态势下达命令（调用方应跳过常规态势）。
        /// </summary>
        private bool TryRespondToDeceptionByDiverting(float now)
        {
            // 仅在 AI 采信到"敌弱"载荷、且门控允许行动时触发（AI-01 主链路的输出）。
            // 注意：窗口关闭不在此处处理——它必须在 Think 的最前面（早于冻结判断），
            // 否则冻结期内本函数根本不会被调用，抽调态势会永续（AI-06⑤ 失效）。
            if (LastGate is not (DecisionGate.Act or DecisionGate.Confident))
            {
                return divertUntilSeconds >= 0f; // 已在抽调窗口内则继续保持
            }

            float believed = sim.LastPayloadAiSaw;
            if (believed < 0f)
            {
                return false;
            }

            bool believesEnemyWeak = believed < 0.75f * sim.EffectiveStrength(Faction.Qin);
            if (!believesEnemyWeak)
            {
                return false;
            }

            var cfg = sim.Rules.AiDeception;

            // 载荷指向的"敌方"（赵军）主力所在方向——即 AI 认为值得压上的方向。
            var zhaoUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout).ToList();
            if (zhaoUnits.Count == 0)
            {
                return false;
            }

            var target = new MapPoint(zhaoUnits.Average(u => u.Position.X), zhaoUnits.Average(u => u.Position.Z));

            // 抽调的"来源方向"＝秦军自己在本阵一侧的守备（抽调后该处战力下降，形成空档）。
            var qinCombat = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsScout && !u.IsRouted).ToList();
            if (qinCombat.Count < cfg.MinUnitsToDivert * 2)
            {
                return false; // 兵力太少，抽不起（否则守备被掏空到荒唐）
            }

            // 空档窗口（AI-06⑤）：到时间后不再保持抽调态势，交由常规态势接管（补位）。
            if (divertUntilSeconds < 0f)
            {
                divertUntilSeconds = now + cfg.WindowSeconds;
                var home = sim.Map.BaseOf(Faction.Qin);
                homeGarrisonAtDecision = qinCombat.Count(u => u.Position.DistanceTo(home) <= 120f);
                sim.LogAi(
                    $"秦军受骗判断「赵军虚弱」——从本阵抽调约 {cfg.DiversionRatio:P0} 兵力压上，" +
                    $"本阵守备由 {homeGarrisonAtDecision} 队降至约 {System.Math.Max(0, homeGarrisonAtDecision - DivertCount(qinCombat.Count, cfg))} 队" +
                    $"（AI-06：空档窗口 {cfg.WindowSeconds:0}s）");
            }
            // 按抽调量分配：一半留守、一半压上载荷指向方向。
            int divert = DivertCount(qinCombat.Count, cfg);
            var home2 = sim.Map.BaseOf(Faction.Qin);
            var garrison = qinCombat.OrderBy(u => u.Position.DistanceTo(home2)).Take(qinCombat.Count - divert).ToList();
            var diverted = qinCombat.Except(garrison).ToList();

            foreach (var unit in diverted)
            {
                // 压上载荷指向方向（AI 相信那里空虚）。
                unit.MoveGoal = sim.Map.ClampToBounds(new MapPoint(
                    target.X + (unit.Position.X < target.X ? -25f : 25f),
                    target.Z));
            }

            foreach (var unit in garrison)
            {
                // 留在本阵一侧守备（这些单位被"留下"，其余被抽走 → 本阵方向出现空档）。
                unit.MoveGoal = sim.Map.ClampToBounds(home2);
            }

            return true;
        }

        /// <summary>抽调单位数（AI-06②）：min(可用兵力 × 抽调比例, 可用兵力 − 守备下限)。</summary>
        private static int DivertCount(int available, Foundation.AI.AiDeceptionConfig cfg)
        {
            int byRatio = (int)System.MathF.Round(available * cfg.DiversionRatio);
            int byFloor = System.Math.Max(1, available - cfg.MinUnitsToDivert);
            return System.Math.Min(byRatio, byFloor);
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
                    unit.MoveGoal = ScoutVantage(allowCrossRiver: false);
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

        /// <summary>
        /// 斥候的前出观察位（**必须考虑丹水的通行约束**）。
        ///
        /// 缺陷背景：原先斥候目标是"赵军质心 ± 偏移"，完全不看地形——
        /// 直线路径撞上丹水（不可通行）后，斥候被 <c>Clamp</c> 永久挡在河边卡死
        /// （实测：秦斥候停在 (17, 50.3) 不动，距赵军 91m，恰好超出 90m 视野 1m，
        ///  于是 AI 情报池恒为空、AI-06 抽调永远不触发）。
        ///
        /// 正确做法：斥候只能**沿着渡口轴线**前出（渡口是唯一过河通道，MAP-11），
        /// 并在河岸一侧停住——这也正好符合 VIS-01"视野需要接近"的设计意图。
        /// </summary>
        private MapPoint ScoutVantage(bool allowCrossRiver)
        {
            var ford = FindFord();
            var enemyBase = sim.Map.BaseOf(Faction.Zhao);

            if (ford is null)
            {
                // 无渡口的地图（教学序章）：退回直接朝敌阵方向，保留原行为。
                return sim.Map.ClampToBounds(enemyBase);
            }

            // 沿渡口轴线**前出到对岸侧**——斥候的职责就是过河侦查，停在自家河岸等于没侦查。
            // 距离纪律：停在"敌方射程之外、但已进入己方视野半径内"的位置。
            // 河道宽 40m，故前出到渡口轴线、对岸侧 offset 处。
            float offset = sim.Rules.ScoutSightRadius * 0.6f;
            float proberZ = ford.Center.Z > enemyBase.Z
                ? ford.Center.Z - offset   // 我方在南岸 → 前出到北岸侧
                : ford.Center.Z + offset;  // 我方在北岸 → 前出到南岸侧

            var vantage = new MapPoint(ford.Center.X, proberZ);
            return sim.Map.ClampToBounds(allowCrossRiver ? enemyBase : vantage);
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
                    unit.MoveGoal = ScoutVantage(allowCrossRiver: false);
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