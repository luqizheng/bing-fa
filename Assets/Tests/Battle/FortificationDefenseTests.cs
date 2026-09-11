using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 壁垒线防御回归（真源 COMBAT-18③）：守方在**己方壁垒缺口段**防御力 ×(1+25%)，攻方不享受。
    ///
    /// 锁定的是**结构规则**（谁享受、在哪享受、与结阵如何并存）；+25% 本身是【初值】，
    /// 调数值不应改本文件——故断言用 `wall.DefenderDefenseBonus` 而非字面量。
    /// </summary>
    public sealed class FortificationDefenseTests
    {
        private static BattleSimulation NewSim() =>
            new(new BattleRules(), autoPlayAi: false, SliceMaps.ChangpingV1);

        private static (BattleMapDefinition Map, FortificationLine Wall, MapPoint Gate) WallInfo()
        {
            var sim = NewSim();
            var wall = sim.Map.Fortifications.Single();
            float gateZ = (wall.GateSpans[0].MinZ + wall.GateSpans[0].MaxZ) * 0.5f;
            return (sim.Map, wall, new MapPoint(wall.FixedX, gateZ));
        }

        [Test]
        public void DefenderAtOwnGate_GetsBonus()
        {
            var (map, wall, gate) = WallInfo();

            Assert.That(map.DefenderDefenseBonusAt(gate, wall.Owner),
                Is.EqualTo(wall.DefenderDefenseBonus).Within(1e-4f),
                "COMBAT-18③：守方在己方壁垒缺口段享防御加成");
        }

        [Test]
        public void AttackerAtGate_GetsNothing()
        {
            var (map, wall, gate) = WallInfo();
            var attacker = wall.Owner == Faction.Zhao ? Faction.Qin : Faction.Zhao;

            Assert.That(map.DefenderDefenseBonusAt(gate, attacker), Is.Zero,
                "COMBAT-18③：攻方在壁垒缺口不享受守方加成（壁垒朝己方一侧才挡箭）");
        }

        [Test]
        public void NonGateSegment_GetsNothing()
        {
            var (map, wall, _) = WallInfo();
            var insideWall = new MapPoint(wall.FixedX, wall.MinZ + 10f);

            Assert.That(map.DefenderDefenseBonusAt(insideWall, wall.Owner), Is.Zero,
                "非缺口段无缺口加成（该处本就不可通行，不该有防御数值）");
        }

        [Test]
        public void DamageAgainstGateDefender_IsLowerThanOpenGround()
        {
            // 同一攻击者、同一守方，仅在"是否位于己方缺口段"上做对照：
            // 缺口加成把 defense 抬高 → 伤害必然更低或持平（COMBAT-08 的减防项变大）。
            var config = CombatConfig.Default;
            float baseDefense = SliceUnitCatalog.Spear.Defense;
            float bonus = SliceMaps.ChangpingV1.Fortifications.Single().DefenderDefenseBonus;

            int openGround = DamageFormula.Resolve(
                panelAttack: SliceUnitCatalog.HeavyInfantry.Attack,
                defense: baseDefense,
                counterCoefficient: 1.35f, // 重步克枪兵（COMBAT-01）
                morale: MoraleState.Normal,
                terrainCoefficient: 1f,
                config: config);

            int atGate = DamageFormula.Resolve(
                panelAttack: SliceUnitCatalog.HeavyInfantry.Attack,
                defense: baseDefense * (1f + bonus),
                counterCoefficient: 1.35f,
                morale: MoraleState.Normal,
                terrainCoefficient: 1f,
                config: config);

            Assert.That(atGate, Is.LessThan(openGround),
                "守方在缺口段时受到的伤害应低于其在开阔地（COMBAT-18③ 的可见效果）");
        }

        [Test]
        public void DamageFloor_StillApplies_UnderGateBonus()
        {
            // 被克 + 缺口加成 → 伤害落到保底 1，不得出现 0 或负数（COMBAT-08 保底）。
            var config = CombatConfig.Default;
            float bonus = SliceMaps.ChangpingV1.Fortifications.Single().DefenderDefenseBonus;
            float defense = SliceUnitCatalog.Spear.Defense * (1f + bonus);

            int dmg = DamageFormula.Resolve(
                panelAttack: SliceUnitCatalog.LightCavalry.Attack, // 轻骑被枪兵克（×0.75）
                defense: defense,
                counterCoefficient: 0.75f,
                morale: MoraleState.Normal,
                terrainCoefficient: 1f,
                config: config);

            Assert.That(dmg, Is.EqualTo(config.MinDamage).Within(1),
                "保底伤害在缺口加成下依然生效（COMBAT-08）");
        }

        [Test]
        public void DeployedDefenderAtGate_ActuallyReceivesBonusInSimulation()
        {
            // 端到端：把一名赵军单位摆到故关缺口，仿真里查询其实际加成 → 必须命中。
            var sim = NewSim();
            var wall = sim.Map.Fortifications.Single();
            float gateZ = (wall.GateSpans[0].MinZ + wall.GateSpans[0].MaxZ) * 0.5f;
            var gate = new MapPoint(wall.FixedX, gateZ);

            var defender = sim.Units.First(u => u.Alive && u.Faction == wall.Owner && !u.IsScout);
            defender.Position = gate;

            Assert.That(sim.Map.TerrainAt(gate), Is.EqualTo(TerrainClass.Passable),
                "缺口段本身可通行（否则守方站不上去）");
            Assert.That(sim.Map.DefenderDefenseBonusAt(defender.Position, defender.Faction),
                Is.EqualTo(wall.DefenderDefenseBonus).Within(1e-4f),
                "站上缺口的守方应实际拿到加成（端到端）");
        }

        [Test]
        public void LegacyMapWithoutWall_HasNoFortificationBonus()
        {
            var sim = new BattleSimulation(new BattleRules(), false, SliceMaps.FukoukouV0);

            Assert.That(sim.Map.Fortifications, Is.Empty, "滏口陉无壁垒（COMBAT-18 不适用）");
            foreach (var unit in sim.Units.Where(u => u.Alive && !u.IsScout))
            {
                Assert.That(sim.Map.DefenderDefenseBonusAt(unit.Position, unit.Faction), Is.Zero,
                    "无壁垒图任何位置都不得出现壁垒加成（避免旧图被新规则误伤）");
            }
        }
    }

}
