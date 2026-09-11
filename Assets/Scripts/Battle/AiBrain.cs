using System.Linq;
using ChinaBettle.AI;
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

            // 目标优先级（AI-02 简化）：采信"敌弱"→ 前压夺点；否则依托野王—高都守势。
            var zhaoUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao).ToList();
            var zhaoCentroid = zhaoUnits.Count > 0
                ? new MapPoint(zhaoUnits.Average(u => u.Position.X), zhaoUnits.Average(u => u.Position.Z))
                : new MapPoint(0f, -140f);

            float believedPlayerStrength = sim.LastPayloadAiSaw;
            bool believesEnemyWeak = believedPlayerStrength >= 0f &&
                                    believedPlayerStrength < 0.75f * sim.EffectiveStrength(Faction.Qin) &&
                                    LastGate is DecisionGate.Act or DecisionGate.Confident;

            // 赵军大帐（夺取关键据点目标，GDD §2.7.1）
            var enemyCamp = sim.Map.Props.First(p => p.Owner == Faction.Zhao && p.Kind == PropKind.Camp);
            var holdLine = new MapPoint(0f, 55f); // 野王—高都之间的守势线

            MapPoint objective = believesEnemyWeak ? enemyCamp.Position : holdLine;

            foreach (var unit in qinUnits)
            {
                if (unit.IsScout)
                {
                    // 斥候前出盯住赵军主力（SLICE-03①：只有斥候目视才产情报）。
                    unit.MoveGoal = sim.Map.Clamp(new MapPoint(
                        zhaoCentroid.X + (unit.Position.X < 0 ? -25f : 25f),
                        zhaoCentroid.Z + 40f));
                    continue;
                }

                // 守势时贴守势线；攻势时前压敌营。弩兵留在后排（射程 180m）。
                float standoff = unit.Definition.HasRangedAttack ? 90f : 5f;
                var goal = unit.Definition.HasRangedAttack
                    ? new MapPoint(objective.X, objective.Z + (believesEnemyWeak ? standoff : standoff))
                    : new MapPoint(objective.X + (unit.Position.X < 0 ? -12f : 12f), objective.Z);

                unit.MoveGoal = sim.Map.Clamp(goal);
            }

            sim.LogAi(believesEnemyWeak
                ? $"秦军采信当前载荷（{believedPlayerStrength:0} 人）判定赵军势弱 → 前压夺赵军大帐（门控 {LastGate}）"
                : $"秦军依托野王—高都守势线固守（门控 {LastGate?.ToString() ?? "无情报"}）");
        }
    }

}