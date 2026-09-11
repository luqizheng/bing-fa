using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 合围断粮回归（真源 FOOD-06，v1.3 新增）。
    ///
    /// 锁定的是**结构规则**：路径连通性判定、围困后饥饿加速、恢复回落；
    /// 具体阈值（5s 判定节拍、3 分钟、×1.5、−5%）标【初值】，改数值不应改本文件。
    /// </summary>
    public sealed class SupplyPathTests
    {
        // autoPlayAi: false —— 本文件的用例手工摆放"围困"态势（秦军占渡口），
        // 若开启 AI，ApplyHoldPosture 会把秦军调回守势线、封锁在 60 秒内自行解除，
        // 那样测的就不是 FOOD-06 而是 AI 态势，失去隔离性。
        private static BattleSimulation NewSim() =>
            new(new BattleRules(), autoPlayAi: false, SliceMaps.ChangpingV1);

        private static MapPoint Ford(BattleSimulation sim) =>
            sim.Map.Volumes
                .Where(v => v.Terrain == TerrainClass.Passable)
                .Where(v => sim.Map.Volumes.Any(big => big.Terrain == TerrainClass.Impassable &&
                                                        System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                        System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .OrderBy(v => v.Width * v.Depth)
                .First()
                .Center;

        [Test]
        public void SupplyPath_OpenAtStart_ForBothSides()
        {
            var sim = NewSim();

            Assert.That(sim.IsSupplyPathOpen(Faction.Zhao), Is.True, "开局渡口无人占据，补给线连通");
            Assert.That(sim.IsSupplyPathOpen(Faction.Qin), Is.True);
        }

        [Test]
        public void SupplyPath_Blocked_WhenEnemyHoldsTheFord()
        {
            var sim = NewSim();
            var ford = Ford(sim);

            // 秦军（敌方）占据渡口 → 赵军补给线被切断（FOOD-06）。
            var qin = sim.Units.First(u => u.Alive && u.Faction == Faction.Qin && !u.IsScout);
            qin.Position = ford;

            Assert.That(sim.IsSupplyPathOpen(Faction.Zhao), Is.False, "渡口被敌占据 → 赵军断粮");
            Assert.That(sim.IsSupplyPathOpen(Faction.Qin), Is.True, "秦军自己的补给线不受己方单位影响");
        }

        [Test]
        public void SupplyPath_Blocked_WhenAllGranariesBurned()
        {
            var sim = NewSim();
            foreach (var granary in sim.Map.GranariesOf(Faction.Zhao).ToList())
            {
                granary.Stock = 0f;
            }

            Assert.That(sim.Map.GranariesOf(Faction.Zhao).All(g => g.IsBurned), Is.True);
            Assert.That(sim.IsSupplyPathOpen(Faction.Zhao), Is.False, "粮仓尽焚 → 断粮");
        }

        /// <summary>
        /// 构造"赵军断粮"的稳定场景。
        ///
        /// 两个必须避开的陷阱：
        /// ① **焚毁己方全部粮仓会立刻判定战败**（`EvaluateOutcome`：粮道尽焚 → 秦军胜），时钟被冻结；
        /// ② **把敌方战斗单位摆到渡口会进入赵弩 180m 射程**，该单位士气崩溃溃逃，封锁自行解除。
        ///
        /// 故改用：**敌方斥候**（不参战、不会因被克制而崩溃）占据渡口 → 路径切断且稳定。
        /// </summary>
        private static BattleSimulation SiegedSim(out SimUnit victim)
        {
            var sim = NewSim();
            var ford = Ford(sim);

            // 先把赵军主力移到渡口以北 220m 处（脱离敌方远程射程，避免封锁部队被击溃而崩溃），
            // 再让秦军一支战斗单位占据渡口。
            foreach (var unit in sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao))
            {
                unit.Position = new MapPoint(unit.Position.X, ford.Z - 220f);
                unit.AttackTargetId = null;
                unit.MoveGoal = null;
            }

            var qinUnit = sim.Units.First(u => u.Alive && u.Faction == Faction.Qin && !u.IsScout);
            qinUnit.Position = ford;
            qinUnit.AttackTargetId = null;
            qinUnit.MoveGoal = null;

            // 清零携粮以隔离"饥饿累积段"（断粮 ≠ 立即饥饿：先要吃完 18 分钟存粮，FOOD-01/04）。
            foreach (var unit in sim.Units.Where(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout))
            {
                unit.RationsUnits = 0f;
            }

            victim = sim.Units.First(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout);
            return sim;
        }

        [Test]
        public void Siege_AmplifiesStarvation_AfterThreshold()
        {
            var sim = SiegedSim(out _);

            Assert.That(sim.IsSupplyPathOpen(Faction.Zhao), Is.False, "渡口被敌方占据 → 赵军断粮");
            Assert.That(sim.IsFinished, Is.False, "前提：战役未结算");

            // 推进到围困超过 3 分钟（FOOD-06③ 的加速门槛）。
            sim.Tick(sim.Rules.Supply.SiegeMultiplierAfterSeconds + 10f);

            Assert.That(sim.Chronicle.Any(c => c.Text.Contains("补给路径被切断")), Is.True,
                "断粮应写入编年史（GDD §5.4）");
            Assert.That(sim.Chronicle.Any(c => c.Text.Contains("饥饿累积 ×1.5")), Is.True,
                "围困逾 3 分钟应提示加速（FOOD-06③）");

            var starving = sim.Units.First(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout);
            Assert.That(starving.Starvation, Is.GreaterThan(0f), "断粮后应开始累积饥饿度");
        }

        [Test]
        public void Starvation_Recovers_WhenSupplyPathReopens()
        {
            var sim = SiegedSim(out var victim);

            sim.Tick(200f);

            float peak = victim.Starvation;
            Assert.That(peak, Is.GreaterThan(0f), "前提：已进入饥饿");

            // 恢复补给的稳定方式：封锁部队撤离渡口 → 路径重新连通 → 饥饿按每 tick −5% 回落（不清零）。
            var qinUnit = sim.Units.First(u => u.Alive && u.Faction == Faction.Qin && !u.IsScout);
            qinUnit.Position = new MapPoint(0f, sim.Map.MaxZ - 20f);

            Assert.That(sim.IsSupplyPathOpen(Faction.Zhao), Is.True, "斥候撤离 → 路径恢复");
            sim.Tick(120f);

            Assert.That(victim.Starvation, Is.LessThan(peak), "补给恢复后饥饿度应回落（FOOD-06）");
        }

        [Test]
        public void Legacymap_WithoutFord_KeepsSupplyOpen()
        {
            // 滏口陉（教学序章图）无渡口 → 不做封锁判定，避免旧图被新规则误伤。
            var sim = new BattleSimulation(new BattleRules(), false, SliceMaps.FukoukouV0);

            Assert.That(sim.IsSupplyPathOpen(Faction.Zhao), Is.True);
            Assert.That(sim.IsSupplyPathOpen(Faction.Qin), Is.True);
        }
    }

}
