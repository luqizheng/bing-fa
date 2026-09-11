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
        private static BattleSimulation NewSim(bool autoPlayAi = false) => new(new BattleRules(), autoPlayAi);

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
            var target = sim.Props.First(p => p.Id == "qin_granary_forward");
            float before = target.Stock;

            // 让一名赵军斥候前出到起火点附近（保证"有视野"分支被走到）。
            var scout = sim.Units.First(u => u.Faction == Faction.Zhao && u.IsScout);
            scout.Position = new MapPoint(0f, 30f);

            Assert.That(sim.TryCast(Faction.Zhao, StratagemId.FireAttack, new MapPoint(0f, 50f)), Is.True);

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
            var victim = sim.Units.First(u => u.Faction == Faction.Qin && !u.IsScout);
            sim.TryCast(Faction.Zhao, StratagemId.FireAttack, new MapPoint(0f, 50f));

            // 把受害者挪到火区中心（(0,50)，半径 26m）。
            victim.Position = new MapPoint(0f, 50f);
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
            var qinUnits = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin).ToList();
            for (int i = 0; i < qinUnits.Count; i++)
            {
                qinUnits[i].Position = new MapPoint(-7f + i * 2f, 100f);
            }

            float trueMen = qinUnits.Count * SimUnit.MenPerUnit;
            var campCentroid = new MapPoint(0f, 100f);

            // 秦军（AI）在自己营地施放增灶示强（AI-05）——被欺骗方是观察秦军的赵军，即玩家。
            Assert.That(sim.TryCast(Faction.Qin, StratagemId.AddStove, campCentroid), Is.True);

            // 玩家先花掉 40 点火攻（SKILL-03），腾出识破奖励的空间
            // （SKILL-11：+15 受 100 上限约束，满点时溢出不累积）。
            Assert.That(sim.TryCast(Faction.Zhao, StratagemId.FireAttack, new MapPoint(0f, 50f)), Is.True);
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
            var sim = NewSim(autoPlayAi: true);

            for (int i = 0; i < 200; i++)
            {
                sim.Tick(6f); // 200 × 6s = 20 战场分钟
            }

            sim.Tick(1f); // 跨过硬上限（浮点累计余量）

            Assert.That(sim.IsFinished, Is.True, "20 分钟硬上限必结算（TIME-04）");
            Assert.That(sim.Chronicle, Is.Not.Empty);
            Assert.That(sim.Chronicle.Any(c => c.Kind == ChronicleKind.Outcome), Is.True);
            Assert.That(sim.Clock.ElapsedSeconds, Is.GreaterThanOrEqualTo(1200f - 0.5f));
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