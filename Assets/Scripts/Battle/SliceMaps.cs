using System.Collections.Generic;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 战役地图库（v1.3 数据化）：全部地图以 <see cref="BattleMapDefinition"/> 数据表达。
    ///
    /// 现役两张：
    /// ① <see cref="ChangpingV1"/> —— **长平之战主战场**（第一张战役地图，真源 MAP-09…16）；
    /// ② <see cref="FukoukouV0"/> —— 太行山·滏口陉（教学序章图，原 SliceMap 内容等价迁移）。
    ///
    /// 几何数值来源：长平图见真源 MAP-09…16；滏口陉图为内容占位（真源未定义几何，等价于原实现）。
    /// </summary>
    public static class SliceMaps
    {
        /// <summary>
        /// 默认地图（P2 起＝长平之战主战场）。
        ///
        /// 注意：返回**新实例**。地图含可变点位状态（<see cref="MapProp.Stock"/> / CaptureProgress），
        /// 若缓存单例会跨场次、跨测试泄漏状态（例：上一局烧掉的粮仓让下一局开局即断粮）。
        /// </summary>
        public static BattleMapDefinition Default => ChangpingV1;

        // ───────────────────────── 长平之战主战场 ─────────────────────────

        /// <summary>
        /// 长平之战主战场（真源 MAP-09…16 / COMBAT-18 / CP-04…05）。
        ///
        /// 地理骨架：南北向河谷 —— 北为赵军西垒（百里石长城壁垒 + 故关），南为秦军东垒（光狼城），
        /// 中间丹水（不可通行）仅渡口可通；西翼韩王山、东翼大粮山为山地减速带，后者是赵军粮仓所在。
        /// 壁垒沿 Z 轴纵向延伸、固定 X＝+20（南北对峙取 Z 轴分区）。
        ///
        /// 坐标口径：X 正向＝东（壁垒方向），Z 正向＝南（赵在北、秦在南）。
        /// </summary>
        public static BattleMapDefinition ChangpingV1 => BuildChangping();

        private static BattleMapDefinition BuildChangping()
        {
            // MAP-09：可玩区域 420m（南北，Z）× 340m（东西，X）
            const float minX = -170f;
            const float maxX = 170f;
            const float minZ = -210f;
            const float maxZ = 210f;

            const float wallX = 20f; // 壁垒线 X（赵军在北，壁垒朝南御秦）

            var volumes = new List<TerrainVolume>
            {
                // 西翼韩王山与东翼大粮山（MAP-13：各约占本方半场 25–35% 的减速带；COMBAT-16 ×0.6）
                new TerrainVolume(new MapPoint(-150f, -80f), 40f, 180f, TerrainClass.Difficult),
                new TerrainVolume(new MapPoint(-150f, 100f), 40f, 100f, TerrainClass.Difficult),
                new TerrainVolume(new MapPoint(150f, -80f), 40f, 180f, TerrainClass.Difficult),
                new TerrainVolume(new MapPoint(150f, 100f), 40f, 100f, TerrainClass.Difficult),

                // 壁垒两翼的密集山林（不可通行：与壁垒共同封住绕行，只留故关缺口）
                new TerrainVolume(new MapPoint(0f, -192f), 340f, 36f, TerrainClass.Impassable),
                new TerrainVolume(new MapPoint(0f, 192f), 340f, 36f, TerrainClass.Impassable),

                // MAP-10：丹水河道 40m 宽横贯全图（不可通行，含赵骑——非减速而是不可通行）
                new TerrainVolume(new MapPoint(0f, 30f), 340f, 40f, TerrainClass.Impassable),

                // MAP-11：丹水渡口 14m（X −7…+7）——河道内唯一可通行处，实现为**略微加高**的可通行体块
                // （嵌套在河道体块内：查询按体积升序，小体块优先，故渡口处返回 Passable）。
                new TerrainVolume(new MapPoint(0f, 30f), 14f, 40f, TerrainClass.Passable),
            };

            var props = new List<MapProp>
            {
                // 赵：西垒后大营 + 大粮山粮仓（CP-05）
                new MapProp { Id = "zhao_camp", DisplayName = "赵军大营", Owner = Faction.Zhao, Kind = PropKind.Camp, Position = new MapPoint(60f, -150f), Radius = 18f },
                new MapProp { Id = "zhao_granary", DisplayName = "赵军大粮山粮仓", Owner = Faction.Zhao, Kind = PropKind.Granary, Position = new MapPoint(130f, -80f), Radius = 9f, InitialStock = 100f, Stock = 100f },
                new MapProp { Id = "zhao_granary_west", DisplayName = "赵军西仓", Owner = Faction.Zhao, Kind = PropKind.Granary, Position = new MapPoint(-70f, -140f), Radius = 9f, InitialStock = 100f, Stock = 100f },

                // 秦：东垒大营 + 光狼城城池/囤积点（CP-05）
                new MapProp { Id = "qin_camp", DisplayName = "秦军大营", Owner = Faction.Qin, Kind = PropKind.Camp, Position = new MapPoint(60f, 150f), Radius = 16f },
                new MapProp { Id = "qin_granary", DisplayName = "秦军光狼城囤积点", Owner = Faction.Qin, Kind = PropKind.Granary, Position = new MapPoint(70f, 120f), Radius = 9f, InitialStock = 100f, Stock = 100f },
                new MapProp { Id = "qin_city", DisplayName = "光狼城", Owner = Faction.Qin, Kind = PropKind.City, Position = new MapPoint(0f, 170f), Radius = 22f },
            };

            var fortifications = new List<FortificationLine>
            {
                // COMBAT-18 / MAP-12：壁垒线长 260m，唯一缺口＝故关 16m（Z −8…+8）
                new FortificationLine(
                    "changping_wall",
                    "百里石长城壁垒（故关）",
                    Faction.Zhao,
                    wallX,
                    -130f,
                    130f,
                    new List<(float, float)> { (-8f, 8f) },
                    DefenderDefenseBonus: 0.25f),
            };

            // 分区沿 Z 轴（南北对峙）；渡口位于丹水河道（Z 10…50）
            var areas = new List<MapArea>
            {
                new MapArea("zhao_rear", "赵军西垒后", float.NegativeInfinity, -60f, AlongX: false),
                new MapArea("west_lei", "西垒·故关", -60f, 10f, AlongX: false),
                new MapArea("crossing", "丹水渡口", 10f, 50f, AlongX: false),
                new MapArea("east_lei", "秦军东垒", 50f, 130f, AlongX: false),
                new MapArea("qin_rear", "光狼城后方", 130f, float.PositiveInfinity, AlongX: false),
            };

            var deployments = new List<MapDeployment>
            {
                // 赵（玩家）：据西垒，壁垒后梯次配置（弩兵在后、骑兵两翼），斥候散出
                // （MAP-15：3 处散出点，此处放 2 名斥候于渡口与故关方向）。
                //
                // 部署纪律：两军弩兵射程 180m、赵骑骑射 150m，故两军主力必须**相距 > 200m 起步**，
                // 否则开局即隔河对射（第 ①② 幕的对峙/桶预养节拍被吃掉，赵军在合围前就被打崩）。
                new MapDeployment(SliceUnitCatalog.ZhaoHuFu, new MapPoint(-60f, -140f), "赵骑1(主将)"),
                new MapDeployment(SliceUnitCatalog.ZhaoHuFu, new MapPoint(90f, -130f), "赵骑2"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(-30f, -90f), "赵枪1"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(10f, -85f), "赵枪2"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(70f, -80f), "赵枪3"),
                new MapDeployment(SliceUnitCatalog.Crossbow, new MapPoint(0f, -60f), "赵弩1"),
                new MapDeployment(SliceUnitCatalog.Crossbow, new MapPoint(40f, -65f), "赵弩2"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(6f, -40f), "赵斥候1"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(-4f, -40f), "赵斥候2"),

                // 秦（AI）：据东垒南岸，与赵军主力保持 200m 以上间隔（同上纪律）。
                new MapDeployment(SliceUnitCatalog.QinRuiShi, new MapPoint(-60f, 150f), "秦锐士1(主将)"),
                new MapDeployment(SliceUnitCatalog.QinRuiShi, new MapPoint(-20f, 150f), "秦锐士2"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(40f, 150f), "秦枪1"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(80f, 150f), "秦枪2"),
                new MapDeployment(SliceUnitCatalog.LightCavalry, new MapPoint(-130f, 155f), "秦骑1"),
                new MapDeployment(SliceUnitCatalog.LightCavalry, new MapPoint(130f, 155f), "秦骑2"),
                new MapDeployment(SliceUnitCatalog.Crossbow, new MapPoint(0f, 168f), "秦弩1"),
                // MAP-15：每方 3 处斥候散出点（渡口 / 故关 / 山地各一）。
                // 数量纪律（真源 INTEL-02）：目击单位修正 = +2% × min(斥候数, 10)。
                // 单斥候只 +2%，而 INTEL-03 需 8 分钟才 +8% —— 叠加基值 65% 后**永远够不到** 70% 的
                // "可信"决策线（实测综合可信度恒在 62–65% 存疑档），AI 于是永远走 ForceRecheck、
                // 永不采信任何载荷，欺骗无从生效。3 名斥候给 +6%，配合持续目击可越过决策线。
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(0f, 95f), "秦斥候1"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(30f, 100f), "秦斥候2"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(-30f, 100f), "秦斥候3"),
            };

            return new BattleMapDefinition(
                id: "changping_v1",
                displayName: "长平之战主战场",
                minX: minX,
                maxX: maxX,
                minZ: minZ,
                maxZ: maxZ,
                volumes: volumes,
                props: props,
                fortifications: fortifications,
                areas: areas,
                deployments: deployments,
                // 赵本阵在壁垒后（大营），秦本阵在东垒
                playerBase: new MapPoint(60f, -150f),
                aiBase: new MapPoint(60f, 150f),
                // AI 守势线：秦军未采信"敌弱"时据守丹水南岸渡口一线
                aiHoldLine: new MapPoint(0f, 70f));
        }

        // ───────────────────────── 教学序章图：滏口陉 ─────────────────────────

        /// <summary>
        /// 太行山·滏口陉（教学序章图）：内容占位，几何与部署**等价于原 <c>SliceMap</c> 实现**
        /// （真源未定义该图几何；见《首张战役地图设计_滏口陉 v0.1》§10 MAP-01…08 待评审）。
        /// 同样每次构造新实例（原因见 <see cref="Default"/>）。
        /// </summary>
        public static BattleMapDefinition FukoukouV0 => BuildFukoukou();

        private static BattleMapDefinition BuildFukoukou()
        {
            const float minX = -110f;
            const float maxX = 110f;
            const float minZ = -170f;
            const float maxZ = 170f;

            var volumes = new List<TerrainVolume>
            {
                // 滏口陉隘口两侧山林（减速通行，COMBAT-16）
                // 隘口内侧山林（缩短到 Z −110…−36，避免与隘口外绝壁体块重叠）
                new TerrainVolume(new MapPoint(-70f, -64f), 80f, 56f, TerrainClass.Difficult),
                new TerrainVolume(new MapPoint(70f, -64f), 80f, 56f, TerrainClass.Difficult),

                // 野王北侧丘陵（合并为整幅，避免两块与高都山林在 X 向重叠）
                new TerrainVolume(new MapPoint(0f, 55f), 88f, 70f, TerrainClass.Difficult),

                // 高都外围山林
                new TerrainVolume(new MapPoint(-75f, 120f), 60f, 90f, TerrainClass.Difficult),
                new TerrainVolume(new MapPoint(75f, 120f), 60f, 90f, TerrainClass.Difficult),

                // 隘口外侧绝壁：原实现以 Clamp 走廊约束表达（|X| ≤ 32，Z −110…−35）——
                // 数据化后等价为两块不可通行体块，只留隘口通行。
                // 注意不得与隘口两侧的减速山林（−110…−30）在 Z 向重叠，故取 Z −110…−70。
                new TerrainVolume(new MapPoint(-71f, -101f), 78f, 18f, TerrainClass.Impassable),
                new TerrainVolume(new MapPoint(71f, -101f), 78f, 18f, TerrainClass.Impassable),
            };

            var props = new List<MapProp>
            {
                // 赵：大帐 + 主仓
                new MapProp { Id = "zhao_camp", DisplayName = "赵军大帐", Owner = Faction.Zhao, Kind = PropKind.Camp, Position = new MapPoint(0f, -150f), Radius = 18f },
                new MapProp { Id = "zhao_granary_main", DisplayName = "赵军主仓", Owner = Faction.Zhao, Kind = PropKind.Granary, Position = new MapPoint(0f, -120f), Radius = 9f, InitialStock = 100f, Stock = 100f },
                new MapProp { Id = "zhao_granary_west", DisplayName = "赵军西仓", Owner = Faction.Zhao, Kind = PropKind.Granary, Position = new MapPoint(-55f, -130f), Radius = 9f, InitialStock = 100f, Stock = 100f },

                // 秦：粮道前仓（火攻目标）+ 主仓 + 高都城池
                new MapProp { Id = "qin_granary_forward", DisplayName = "秦军粮道前仓", Owner = Faction.Qin, Kind = PropKind.Granary, Position = new MapPoint(0f, 50f), Radius = 9f, InitialStock = 100f, Stock = 100f },
                new MapProp { Id = "qin_granary_main", DisplayName = "秦军主仓", Owner = Faction.Qin, Kind = PropKind.Granary, Position = new MapPoint(45f, 120f), Radius = 9f, InitialStock = 100f, Stock = 100f },
                new MapProp { Id = "qin_city", DisplayName = "高都", Owner = Faction.Qin, Kind = PropKind.City, Position = new MapPoint(0f, 145f), Radius = 22f },
                new MapProp { Id = "qin_camp", DisplayName = "秦军大帐", Owner = Faction.Qin, Kind = PropKind.Camp, Position = new MapPoint(0f, 115f), Radius = 16f },
            };

            var areas = new List<MapArea>
            {
                new MapArea("zhao_rear", "赵军后方", float.NegativeInfinity, -105f, AlongX: false),
                new MapArea("choke", "滏口陉", -105f, -30f, AlongX: false),
                new MapArea("yewang", "野王", -30f, 65f, AlongX: false),
                new MapArea("qin_forward", "秦军前哨", 65f, 105f, AlongX: false),
                new MapArea("gaodu", "高都", 105f, float.PositiveInfinity, AlongX: false),
            };

            var deployments = new List<MapDeployment>
            {
                new MapDeployment(SliceUnitCatalog.ZhaoHuFu, new MapPoint(-16f, -140f), "赵骑1(主将)"),
                new MapDeployment(SliceUnitCatalog.ZhaoHuFu, new MapPoint(16f, -140f), "赵骑2"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(-30f, -150f), "赵枪1"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(0f, -152f), "赵枪2"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(30f, -150f), "赵枪3"),
                new MapDeployment(SliceUnitCatalog.Crossbow, new MapPoint(-12f, -158f), "赵弩1"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(-40f, -128f), "赵斥候1"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(40f, -128f), "赵斥候2"),

                new MapDeployment(SliceUnitCatalog.QinRuiShi, new MapPoint(-18f, 100f), "秦锐士1(主将)"),
                new MapDeployment(SliceUnitCatalog.QinRuiShi, new MapPoint(18f, 100f), "秦锐士2"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(-36f, 92f), "秦枪1"),
                new MapDeployment(SliceUnitCatalog.Spear, new MapPoint(36f, 92f), "秦枪2"),
                new MapDeployment(SliceUnitCatalog.LightCavalry, new MapPoint(-52f, 108f), "秦骑1"),
                new MapDeployment(SliceUnitCatalog.LightCavalry, new MapPoint(52f, 108f), "秦骑2"),
                new MapDeployment(SliceUnitCatalog.Crossbow, new MapPoint(0f, 84f), "秦弩1"),
                new MapDeployment(SliceUnitCatalog.Scout, new MapPoint(0f, 70f), "秦斥候1"),
            };

            return new BattleMapDefinition(
                id: "fukoukou_v0",
                displayName: "太行山·滏口陉",
                minX: minX,
                maxX: maxX,
                minZ: minZ,
                maxZ: maxZ,
                volumes: volumes,
                props: props,
                fortifications: new List<FortificationLine>(),
                areas: areas,
                deployments: deployments,
                playerBase: new MapPoint(0f, -150f),
                aiBase: new MapPoint(0f, 115f),
                aiHoldLine: new MapPoint(0f, 55f));
        }
    }

}
