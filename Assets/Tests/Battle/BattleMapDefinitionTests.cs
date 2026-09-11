using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 地图定义回归（P2 数据化；真源 MAP-09…16、COMBAT-05/16/18、CP-04/05）。
    ///
    /// 纪律：这些用例锁定**地图结构规则**（通行等级、壁垒缺口、区域划分、不可穿越），
    /// 不锁定具体几何数字——几何仍标【拟设】待评审（真源表 §12），改数值不应改本文件；
    /// 但"壁垒非缺口不可通行""渡口是唯一过河处""部署不在不可通行地形"这三类**结构性质**必须长期成立。
    /// </summary>
    public sealed class BattleMapDefinitionTests
    {
        private static BattleMapDefinition Changping => SliceMaps.ChangpingV1;

        // ───────────────────────── COMBAT-18：壁垒线 ─────────────────────────

        [Test]
        public void Fortification_GateIsPassable_NonGateIsImpassable()
        {
            var map = Changping;
            var wall = map.Fortifications.Single();
            float gateZ = (wall.GateSpans[0].MinZ + wall.GateSpans[0].MaxZ) * 0.5f;

            Assert.That(map.TerrainAt(new MapPoint(wall.FixedX, gateZ)), Is.EqualTo(TerrainClass.Passable),
                "COMBAT-18②：缺口段可通行");

            Assert.That(map.TerrainAt(new MapPoint(wall.FixedX, wall.MinZ + 10f)), Is.EqualTo(TerrainClass.Impassable),
                "COMBAT-18①：壁垒非缺口段不可通行");
            Assert.That(map.TerrainAt(new MapPoint(wall.FixedX, wall.MaxZ - 10f)), Is.EqualTo(TerrainClass.Impassable),
                "COMBAT-18①：壁垒两端同为不可通行");
        }

        [Test]
        public void Fortification_DefenseBonus_AppliesToDefenderOnly()
        {
            var map = Changping;
            var wall = map.Fortifications.Single();
            float gateZ = (wall.GateSpans[0].MinZ + wall.GateSpans[0].MaxZ) * 0.5f;
            var gatePoint = new MapPoint(wall.FixedX, gateZ);

            Assert.That(map.DefenderDefenseBonusAt(gatePoint, wall.Owner), Is.EqualTo(wall.DefenderDefenseBonus).Within(1e-4f),
                "COMBAT-18③：守方在己方壁垒缺口享防御加成");
            Assert.That(map.DefenderDefenseBonusAt(gatePoint, OpponentOf(wall.Owner)), Is.Zero,
                "COMBAT-18③：攻方不享受");

            var insideWall = new MapPoint(wall.FixedX, wall.MinZ + 10f);
            Assert.That(map.DefenderDefenseBonusAt(insideWall, wall.Owner), Is.Zero,
                "非缺口段无缺口加成（该处本就不可通行）");
        }

        [Test]
        public void Fortification_BlocksAdvance_ButGateAllowsIt()
        {
            var map = Changping;
            var wall = map.Fortifications.Single();
            float gateZ = (wall.GateSpans[0].MinZ + wall.GateSpans[0].MaxZ) * 0.5f;

            // 非缺口段：沿 X 反复推进被挡在壁垒外侧。
            var blocked = new MapPoint(wall.FixedX - 40f, wall.MinZ + 30f);
            for (int i = 0; i < 20; i++)
            {
                blocked = map.Clamp(blocked, new MapPoint(blocked.X + 5f, blocked.Z));
            }

            Assert.That(blocked.X, Is.LessThan(wall.FixedX), "COMBAT-18①：非缺口段无法穿越壁垒");

            // 缺口段：同样推进可以穿到壁垒另一侧。
            var through = new MapPoint(wall.FixedX - 40f, gateZ);
            for (int i = 0; i < 20; i++)
            {
                through = map.Clamp(through, new MapPoint(through.X + 5f, through.Z));
            }

            Assert.That(through.X, Is.GreaterThan(wall.FixedX + 20f), "COMBAT-18②：缺口段可以穿行");
        }

        // ───────────────────────── MAP-10/11：丹水与渡口 ─────────────────────────

        [Test]
        public void River_IsImpassable_ExceptAtTheFord()
        {
            var map = Changping;

            // 河道带：所有体块中面积最大的不可通行横带即丹水；取其中点与渡口对比。
            var impassable = map.Volumes.Where(v => v.Terrain == TerrainClass.Impassable).ToList();
            Assert.That(impassable, Is.Not.Empty, "长平图应含不可通行地形（丹水/边界绝壁）");

            bool foundFord = false;
            foreach (var vol in map.Volumes.Where(v => v.Terrain == TerrainClass.Passable))
            {
                // 渡口＝嵌在不可通行体块内部的可通行小体块（MAP-11）。
                bool nestedInImpassable = impassable.Any(big =>
                    vol.Center.X - vol.Width * 0.5f >= big.Center.X - big.Width * 0.5f &&
                    vol.Center.X + vol.Width * 0.5f <= big.Center.X + big.Width * 0.5f &&
                    vol.Center.Z - vol.Depth * 0.5f >= big.Center.Z - big.Depth * 0.5f &&
                    vol.Center.Z + vol.Depth * 0.5f <= big.Center.Z + big.Depth * 0.5f);

                if (!nestedInImpassable)
                {
                    continue;
                }

                foundFord = true;
                Assert.That(map.TerrainAt(vol.Center), Is.EqualTo(TerrainClass.Passable),
                    "MAP-11：渡口（嵌套可通行体块）本身可通行");
            }

            Assert.That(foundFord, Is.True, "MAP-11：丹水应存在唯一可通行的渡口体块");
        }

        [Test]
        public void River_CannotBeCrossed_OutsideTheFord()
        {
            var map = Changping;
            var ford = map.Volumes
                .Where(v => v.Terrain == TerrainClass.Passable)
                .OrderBy(v => v.Width * v.Depth)
                .First();

            // 渡口正轴：可逐步南下过河。
            var viaFord = new MapPoint(ford.Center.X, ford.Center.Z - ford.Depth * 0.5f - 10f);
            for (int i = 0; i < 40; i++)
            {
                viaFord = map.Clamp(viaFord, new MapPoint(viaFord.X, viaFord.Z + 5f));
            }

            Assert.That(viaFord.Z, Is.GreaterThan(ford.Center.Z + ford.Depth * 0.5f),
                "MAP-11：渡口处可以过河");

            // 远离渡口的同一河道带：被拦停。
            float offAxisX = ford.Center.X + 80f;
            var northBank = new MapPoint(offAxisX, ford.Center.Z - ford.Depth * 0.5f - 10f);
            Assert.That(map.TerrainAt(northBank), Is.Not.EqualTo(TerrainClass.Impassable), "取点应在河北岸可通行处");

            var blocked = northBank;
            for (int i = 0; i < 40; i++)
            {
                blocked = map.Clamp(blocked, new MapPoint(blocked.X, blocked.Z + 5f));
            }

            Assert.That(blocked.Z, Is.LessThanOrEqualTo(ford.Center.Z - ford.Depth * 0.5f + 1e-3f),
                "MAP-10：渡口以外无法跨越丹水");
        }

        // ───────────────────────── 结构与部署 ─────────────────────────

        [Test]
        public void Areas_CoverTheWholeMap_AndAreOrdered()
        {
            var map = Changping;

            Assert.That(map.AreaIdAt(new MapPoint(0f, map.MinZ + 1f)), Is.EqualTo("zhao_rear"),
                "最北端属赵军后方区域");
            Assert.That(map.AreaIdAt(new MapPoint(0f, map.MaxZ - 1f)), Is.EqualTo("qin_rear"),
                "最南端属光狼城后方区域");

            foreach (var area in map.Areas)
            {
                Assert.That(map.AreaDisplayName(area.Id), Is.Not.Empty, $"区域 {area.Id} 应有展示名");
            }
        }

        [Test]
        public void Deployments_NeverStartOnImpassableTerrain()
        {
            foreach (var map in new[] { SliceMaps.ChangpingV1, SliceMaps.FukoukouV0 })
            {
                Assert.That(map.Deployments, Is.Not.Empty, $"{map.Id} 应有初始部署（CP-05）");

                foreach (var deployment in map.Deployments)
                {
                    var terrain = map.TerrainAt(deployment.Position);
                    Assert.That(terrain, Is.Not.EqualTo(TerrainClass.Impassable),
                        $"{map.Id}：{deployment.Label} @{deployment.Position} 不得出生在不可通行地形（否则开局即被困）");
                }
            }
        }

        [Test]
        public void Deployments_BothSides_HaveBaseAndGranary()
        {
            foreach (var map in new[] { SliceMaps.ChangpingV1, SliceMaps.FukoukouV0 })
            {
                foreach (var faction in new[] { Faction.Zhao, Faction.Qin })
                {
                    Assert.That(map.Deployments.Any(d => map.TerrainAt(d.Position) != TerrainClass.Impassable),
                        $"{map.Id} 应有部署");
                    Assert.That(map.GranariesOf(faction).Any(), Is.True, $"{map.Id}：{faction} 应有粮仓（FOOD-03）");
                    Assert.That(map.BaseOf(faction), Is.EqualTo(map.CampOf(faction)!.Position),
                        $"{map.Id}：{faction} 溃逃归队点＝本阵大帐");
                    Assert.That(map.TerrainAt(map.BaseOf(faction)), Is.Not.EqualTo(TerrainClass.Impassable),
                        $"{map.Id}：{faction} 本阵不得在不可通行地形");
                }
            }
        }

        [Test]
        public void TerrainVolumes_DoNotUnintentionallyOverlap()
        {
            // 允许"嵌套"（通行口开在不可通行区内），但**不允许部分交叠**——
            // 部分交叠会让 TerrainAt 的结果取决于扫描顺序，属隐性耦合。
            foreach (var map in new[] { SliceMaps.ChangpingV1, SliceMaps.FukoukouV0 })
            {
                var volumes = map.Volumes;
                for (int i = 0; i < volumes.Count; i++)
                {
                    for (int j = i + 1; j < volumes.Count; j++)
                    {
                        var a = volumes[i];
                        var b = volumes[j];
                        if (!Intersects(a, b))
                        {
                            continue;
                        }

                        bool nested = Contains(a, b) || Contains(b, a);
                        Assert.That(nested, Is.True,
                            $"{map.Id}：体块 {a.Center} 与 {b.Center} 部分交叠（只允许完全嵌套）");
                    }
                }
            }
        }

        [Test]
        public void LegacyMap_Fukoukou_KeepsItsCharacter()
        {
            var map = SliceMaps.FukoukouV0;

            Assert.That(map.Fortifications, Is.Empty, "滏口陉无壁垒线（COMBAT-18 不适用于该图）");
            Assert.That(map.AreaIdAt(new MapPoint(0f, -75f)), Is.EqualTo("choke"), "隘口分区仍在");
            Assert.That(map.GranariesOf(Faction.Qin).Count(), Is.EqualTo(2), "秦军两处粮仓（前仓 + 主仓）");
        }

        private static bool Intersects(TerrainVolume a, TerrainVolume b) =>
            a.Center.X - a.Width * 0.5f < b.Center.X + b.Width * 0.5f &&
            b.Center.X - b.Width * 0.5f < a.Center.X + a.Width * 0.5f &&
            a.Center.Z - a.Depth * 0.5f < b.Center.Z + b.Depth * 0.5f &&
            b.Center.Z - b.Depth * 0.5f < a.Center.Z + a.Depth * 0.5f;

        private static bool Contains(TerrainVolume outer, TerrainVolume inner) =>
            inner.Center.X - inner.Width * 0.5f >= outer.Center.X - outer.Width * 0.5f &&
            inner.Center.X + inner.Width * 0.5f <= outer.Center.X + outer.Width * 0.5f &&
            inner.Center.Z - inner.Depth * 0.5f >= outer.Center.Z - outer.Depth * 0.5f &&
            inner.Center.Z + inner.Depth * 0.5f <= outer.Center.Z + outer.Depth * 0.5f;

        private static Faction OpponentOf(Faction faction) => faction == Faction.Zhao ? Faction.Qin : Faction.Zhao;
    }

}
