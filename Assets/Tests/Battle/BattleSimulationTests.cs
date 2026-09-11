using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Stratagems;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 垂直切片仿真回归（SLICE-01~04；真源 COMBAT/FOOD/DECP/SKILL/AI 系列）。
    /// 覆盖：单位模板数值、结阵反骑、火攻与火区、招牌欺骗链路（篡改→近距识破→桶置 0.2→+15 谋略点）、
    /// 胜负判定与 20 分钟长跑稳定性。
    /// </summary>
    public sealed class BattleSimulationTests
    {
        // P2 起默认地图＝长平之战主战场（SliceMaps.Default）。测试一律**按语义取点**
        // （取"敌方粮仓""敌方单位"等对象），不硬编码坐标与 prop id——地图数据变化不应改测试。
        private static BattleSimulation NewSim(bool autoPlayAi = false) => new(new BattleRules(), autoPlayAi);

        private static BattleSimulation NewSimOn(BattleMapDefinition map, bool autoPlayAi = false) =>
            new(new BattleRules(), autoPlayAi, map);

        /// <summary>敌方第一处未焚毁粮仓（火攻的合法目标）。</summary>
        private static MapProp EnemyGranary(BattleSimulation sim, Faction caster) =>
            sim.Props.First(p => p.Owner != caster && p.Kind == PropKind.Granary && !p.IsBurned);


        /// <summary>
        /// 取一处"开阔平原"坐标（无地形体块覆盖、可通行），供把部队聚拢成营盘的测试使用。
        ///
        /// **选址纪律**：以 <paramref name="awayFrom"/>（通常为敌方本阵）为中心向外找，
        /// 且要求该点距**敌方所有存活单位**至少 <paramref name="minEnemyDistance"/> 米——
        /// 否则聚拢后的营地会落在敌方阵中，开局即接战减员，令测试隔离性失效。
        /// </summary>
        private static MapPoint OpenGround(BattleSimulation sim, MapPoint awayFrom, Faction enemyOf, float minEnemyDistance)
        {
            var map = sim.Map;
            for (float radius = 40f; radius <= 260f; radius += 20f)
            {
                for (int step = 0; step < 32; step++)
                {
                    float angle = step / 32f * System.MathF.PI * 2f;
                    var p = new MapPoint(
                        awayFrom.X + System.MathF.Cos(angle) * radius,
                        awayFrom.Z + System.MathF.Sin(angle) * radius);

                    if (map.TerrainAt(p) != TerrainClass.Passable)
                    {
                        continue;
                    }

                    bool clear = true;
                    for (int dx = -40; dx <= 40 && clear; dx += 10)
                    {
                        for (int dz = -40; dz <= 40 && clear; dz += 10)
                        {
                            if (map.TerrainAt(new MapPoint(p.X + dx, p.Z + dz)) != TerrainClass.Passable)
                            {
                                clear = false;
                            }
                        }
                    }

                    if (!clear)
                    {
                        continue;
                    }

                    bool farEnough = sim.Units
                        .Where(u => u.Alive && u.Faction == enemyOf)
                        .All(u => u.Position.DistanceTo(p) >= minEnemyDistance);

                    if (farEnough)
                    {
                        return p;
                    }
                }
            }

            return MapPoint.Zero;
        }

        [Test]
        public void UnitCatalog_MatchesSpecNumbers()
        {
            // 兵种表 v0.2 §1/§2 抽样核对（COMBAT-14 真源数值）
            Assert.That(SliceUnitCatalog.Spear.MaxHealth, Is.EqualTo(120f));
            Assert.That(SliceUnitCatalog.Crossbow.AttackRange, Is.EqualTo(180f));
            Assert.That(SliceUnitCatalog.QinRuiShi.MaxHealth, Is.EqualTo(200f));
            Assert.That(SliceUnitCatalog.ZhaoHuFu.RangedAttack, Is.EqualTo(12f));
            Assert.That(SliceUnitCatalog.ZhaoHuFu.RangedRange, Is.EqualTo(150f));
            Assert.That(SliceUnitCatalog.ZhaoHuFu.IgnoresDifficultTerrain, Is.True);
        }

        [Test]
        public void DeployedForces_BothSides_HaveUnitsAndProps()
        {
            var sim = NewSim();

            Assert.That(sim.Units.Count(u => u.Faction == Faction.Zhao), Is.EqualTo(9));
            Assert.That(sim.Units.Count(u => u.Faction == Faction.Qin), Is.EqualTo(8));
            Assert.That(sim.Units.Any(u => u.Faction == Faction.Zhao && u.IsScout), Is.True);
            Assert.That(sim.Props.Any(p => p.Kind == PropKind.Granary && p.Owner == Faction.Qin), Is.True);
            Assert.That(sim.Clock.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void DifficultTerrain_SlowsNonZhaoCavalry_ButNotZhaoHuFu()
        {
            var rules = new BattleRules();
            var spear = new SimUnit("s", Faction.Zhao, SliceUnitCatalog.Spear, MapPoint.Zero, rules);
            var huFu = new SimUnit("h", Faction.Zhao, SliceUnitCatalog.ZhaoHuFu, MapPoint.Zero, rules);

            // COMBAT-16：减速地形内非赵骑 ×0.6；赵骑忽略（COMBAT-06）。
            Assert.That(spear.CurrentMoveSpeed(rules, TerrainClass.Difficult), Is.EqualTo(3.5f * 0.6f * 0.8f).Within(1e-4f),
                "枪兵结阵默认开：3.5 × 0.6 × 0.8");
            Assert.That(huFu.CurrentMoveSpeed(rules, TerrainClass.Difficult), Is.EqualTo(7.5f).Within(1e-4f),
                "赵骑忽略减速地形且非结阵");
            Assert.That(spear.CurrentMoveSpeed(rules, TerrainClass.Passable), Is.EqualTo(3.5f * 0.8f).Within(1e-4f));
        }

        [Test]
        public void FormationReflect_PunishesCavalryHarassingFormedSpear()
        {
            var rules = new BattleRules();
            var spear = new SimUnit("s", Faction.Zhao, SliceUnitCatalog.Spear, MapPoint.Zero, rules);
            var zhaoHuFu = new SimUnit("h", Faction.Qin, SliceUnitCatalog.ZhaoHuFu, new MapPoint(0f, 100f), rules);

            Assert.That(FormationRules.CavalryReflectDamage(zhaoHuFu.IsCavalry, spear.Formed && spear.Definition.SpearFormationCapable, rules.Combat),
                Is.EqualTo(6), "COMBAT-17③：骑兵骑射结阵枪兵 → 6 点/发反噬");

            // 解阵后不再反噬（僵持成本消失）。
            spear.Formed = false;
            Assert.That(FormationRules.CavalryReflectDamage(zhaoHuFu.IsCavalry, spear.Formed && spear.Definition.SpearFormationCapable, rules.Combat),
                Is.Zero);
        }

        [Test]
        public void FireAttack_Burns40PercentGranary_CreatesFireZone_AndSkillRevealIntel()
        {
            var sim = NewSim();
            var target = EnemyGranary(sim, Faction.Zhao);
            float before = target.Stock;

            // 让一名赵军斥候前出到起火点附近（保证"有视野"分支被走到）。
            var scout = sim.Units.First(u => u.Faction == Faction.Zhao && u.IsScout);
            scout.Position = target.Position;

            Assert.That(sim.TryCast(Faction.Zhao, StratagemId.FireAttack, target.Position), Is.True);

            Assert.That(target.Stock, Is.EqualTo(before * 0.6f).Within(1e-3f), "SKILL-03：立即销毁 40% 粮草");
            Assert.That(sim.FireZones, Has.Count.EqualTo(1), "形成火区");
            Assert.That(sim.FireZones[0].EndSeconds - sim.FireZones[0].CreatedAtSeconds, Is.EqualTo(60f).Within(1e-4f),
                "火区持续 60 战场秒");
            Assert.That(sim.PlayerIntel.Records.Any(r => r.SourceType == IntelSourceType.SkillReveal), Is.True,
                "SLICE-03⑤：有视野方获得 skill_reveal 真实情报");
            Assert.That(sim.Stats.PlayerCasts, Is.EqualTo(1));
            Assert.That(sim.PlayerPoints.Points, Is.EqualTo(100 - 40), "火攻 40 点（SKILL-03）");
        }

        [Test]
        public void FireZone_BurnsUnitsInside()
        {
            var sim = NewSim();
            var target = EnemyGranary(sim, Faction.Zhao);
            var victim = sim.Units.First(u => u.Faction == Faction.Qin && !u.IsScout);
            sim.TryCast(Faction.Zhao, StratagemId.FireAttack, target.Position);

            // 把受害者挪到火区中心（火区以起火点＝粮仓为中心）。
            victim.Position = target.Position;
            float healthBefore = victim.Health;

            sim.Tick(6.1f); // 6 秒 → 3 个 tick × 8 = 24 点

            Assert.That(victim.Health, Is.LessThan(healthBefore), "火区内单位持续灼烧（SKILL-03）");
            Assert.That(healthBefore - victim.Health, Is.EqualTo(24f).Within(1e-3f));
        }

        [Test]
        public void DeceptionChain_TamperedPayload_ThenProximityDetection_ResetsBucketAndAwards15()
        {
            var sim = NewSim();
            var rules = sim.Rules;

            // 把秦军聚拢成一处营盘（把"观察目标集"固定下来，隔离变量），并让斥候扛住弩兵射击。
            // 营盘选址＝大地图内一处可通行平原（取秦军本阵一侧，避开壁垒带与河道）。
            var qinUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin).ToList();
            // 营盘选址：在**秦军本阵一侧**离**赵军**至少 150m 的开阔地（避免开局即接战减员，
            // 隔离"观察-欺骗"链路）。坐标不硬编码，随地图数据自适应。
            var campCentroid = OpenGround(sim, sim.Map.BaseOf(Faction.Qin), Faction.Zhao, 150f);
            for (int i = 0; i < qinUnits.Count; i++)
            {
                qinUnits[i].Position = new MapPoint(campCentroid.X - 7f + i * 2f, campCentroid.Z);
            }

            float trueMen = qinUnits.Count * SimUnit.MenPerUnit;

            // 秦军（AI）在自己营地施放增灶示强（AI-05）——被欺骗方是观察秦军的赵军，即玩家。
            Assert.That(sim.TryCast(Faction.Qin, StratagemId.AddStove, campCentroid), Is.True);

            // 玩家先花掉 40 点火攻（SKILL-03），腾出识破奖励的空间
            // （SKILL-11：+15 受 100 上限约束，满点时溢出不累积）。
            Assert.That(sim.TryCast(Faction.Zhao, StratagemId.FireAttack, EnemyGranary(sim, Faction.Zhao).Position), Is.True);
            Assert.That(sim.PlayerPoints.Points, Is.EqualTo(60));

            var scout = sim.Units.First(u => u.Faction == Faction.Zhao && u.IsScout);
            scout.Health = 500f; // 斥候暴露在弩兵射程内，此处只为隔离变量、保证观察节拍能跑到
            // 远观采样：进入视野（90m）但不在 50m 近距清点范围内 → 吃到篡改载荷。
            scout.Position = new MapPoint(campCentroid.X + 70f, campCentroid.Z);

            sim.Tick(rules.ObservationIntervalSeconds + 0.2f);

            var feed = sim.PlayerIntelFeed(sim.Clock.ElapsedSeconds);
            var first = feed.FirstOrDefault();
            Assert.That(first, Is.Not.Null, "斥候目视应生成情报（SLICE-03①）");
            Assert.That(first!.Value, Is.EqualTo(trueMen * 1.5f).Within(1f), "增灶篡改：远观载荷 ×1.5（DECP-03）");

            // 近距 50 米内清点 → 识破路径①：看到真值、条目标伪、AI 该主题桶置 0.20、玩家 +15 谋略点。
            int pointsBefore = sim.PlayerPoints.Points;
            scout.Position = new MapPoint(campCentroid.X + 30f, campCentroid.Z);
            sim.Tick(rules.ObservationIntervalSeconds + 0.2f);

            var afterDetection = sim.PlayerIntelFeed(sim.Clock.ElapsedSeconds);
            Assert.That(afterDetection.Any(i => i.ShowFakeStamp), Is.True, "UI-03：识破条目带'？'标记");
            Assert.That(afterDetection.First().Value, Is.EqualTo(trueMen).Within(1f), "近距清点看到真值（DECP-04）");
            Assert.That(sim.AiTrust.Get(sim.PlayerTopic), Is.EqualTo(0.20f).Within(1e-4f), "TRUST-06：识破置 0.20");
            Assert.That(sim.PlayerPoints.Points, Is.EqualTo(pointsBefore + 15), "SKILL-11：识破 +15");
            Assert.That(sim.Stats.PlayerDetections, Is.EqualTo(1));
        }

        [Test]
        public void AiDeception_RegisteredWithSameRules()
        {
            var sim = NewSim(autoPlayAi: true);
            sim.Tick(120f);

            // AI 在被判定兵力劣势或可设伏时释放欺骗（AI-05），与玩家同规则（痕迹 + 谋略点）。
            bool aiCastSomething = sim.Chronicle.Any(c => c.Kind == ChronicleKind.Cast && c.Text.StartsWith("秦军施放"));
            Assert.That(aiCastSomething || sim.AiPoints.Points == 100, Is.True,
                "AI 若无触发条件则不应扣点；一旦施计必须走同规则记账");
        }

        [Test]
        public void Annihilation_EndsBattle()
        {
            var sim = NewSim();
            foreach (var unit in sim.Units.Where(u => u.Faction == Faction.Qin).ToList())
            {
                unit.Removed = true;
            }

            sim.Tick(0.2f);

            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.ZhaoVictory));
            Assert.That(sim.OutcomeReason, Does.Contain("歼灭"));
        }

        [Test]
        public void TwentyMinuteRun_ReachesOutcome_WithoutExceptions()
        {
            // 语义（P3 起）：剧本（CP-08）可提前给出结局；本用例锁定的是
            // ①**不会**抛异常、②**要么**提前结算**要么**撑满 20 分钟硬上限（TIME-04），不许两者皆非。
            var sim = NewSim(autoPlayAi: true);

            for (int i = 0; i < 200 && !sim.IsFinished; i++)
            {
                sim.Tick(6f); // 200 × 6s = 20 战场分钟
            }

            sim.Tick(1f); // 跨过硬上限（浮点累计余量）

            Assert.That(sim.IsFinished, Is.True, "提前结算或 20 分钟硬上限，二者必居其一（TIME-04/CP-08）");
            Assert.That(sim.Chronicle, Is.Not.Empty);
            Assert.That(sim.Chronicle.Any(c => c.Kind == ChronicleKind.Outcome), Is.True);

            bool endedEarly = sim.Clock.ElapsedSeconds < 1200f - 0.5f;
            if (endedEarly)
            {
                // 提前结算只允许来自剧本结局（CP-08）或歼灭/焚粮（GDD §2.7.1）。
                Assert.That(
                    sim.OutcomeReason.Contains("结局") || sim.OutcomeReason.Contains("歼灭") || sim.OutcomeReason.Contains("焚"),
                    Is.True,
                    $"提前结算必须是合法结局，实际：{sim.OutcomeReason}");
            }
            else
            {
                Assert.That(sim.Clock.ElapsedSeconds, Is.GreaterThanOrEqualTo(1200f - 0.5f), "未提前结算则必须撑满硬上限");
            }
        }

        [Test]
        public void PausedClock_FreezesAllSystems()
        {
            var sim = NewSim();
            sim.Clock.Pause();
            float before = sim.Clock.ElapsedSeconds;
            int chronicleBefore = sim.Chronicle.Count;

            sim.Tick(30f);

            Assert.That(sim.Clock.ElapsedSeconds, Is.EqualTo(before), "TIME-02：暂停冻结全部战役计时");
            Assert.That(sim.Chronicle.Count, Is.EqualTo(chronicleBefore));
        }
    }
}