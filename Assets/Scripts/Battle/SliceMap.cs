using System.Collections.Generic;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>地图物件类型。</summary>
    public enum PropKind
    {
        /// <summary>粮草囤积点（FOOD-03/火攻目标）。</summary>
        Granary,

        /// <summary>大帐/营地（据点争夺目标）。</summary>
        Camp,

        /// <summary>城池（高都，秦据点）。</summary>
        City,
    }

    /// <summary>
    /// 地图物件：粮仓/大帐/城池。
    /// <paramref name="Stock"/> 仅粮仓有意义（初始粮草单位数，FIRE 火攻按 40% 递减，SKILL-03）。
    /// </summary>
    public sealed class MapProp
    {
        public string Id { get; init; }

        public string DisplayName { get; init; }

        public Faction Owner { get; init; }

        public PropKind Kind { get; init; }

        public MapPoint Position { get; init; }

        public float Radius { get; init; }

        public float InitialStock { get; init; }

        public float Stock { get; set; }

        /// <summary>占领进度（秒）：敌方单位在半径内持续累积（据点胜利判定）。</summary>
        public float CaptureProgress { get; set; }

        /// <summary>
        /// 是否已被焚毁（火攻焚毁判定）。
        /// 阈值口径：粮草存量 ≤ 初始的 20% 视为焚毁不可用——**建筑/粮仓生命与摧毁阈值待 v0.3 定义**
        /// （《基础兵种数值表 v0.2》§5），此处为切片占位口径，定案后回写真源。
        /// </summary>
        public bool IsBurned => Kind == PropKind.Granary && Stock <= InitialStock * BurnedRatio;

        public const float BurnedRatio = 0.20f;
    }

    /// <summary>地形块（矩形）：减速通行覆盖区（COMBAT-16）。</summary>
    public sealed record TerrainPatch(MapPoint Center, float Width, float Depth, TerrainClass Terrain);

    /// <summary>
    /// 切片地图"太行山·滏口陉"（SLICE-01）：滏口陉–野王–高都走廊。
    /// 布局为**内容占位**（真源表未定义地图几何）：
    ///   赵军在南（z≈−140），北出滏口陉隘口（两侧山林 = 减速通行），穿野王平原，逼向秦之高都（z≈+140）。
    /// 秦军在粮道中段设前仓（火攻目标），后方有主仓与城池。
    /// </summary>
    public sealed class SliceMap
    {
        public const float MinX = -110f;
        public const float MaxX = 110f;
        public const float MinZ = -170f;
        public const float MaxZ = 170f;

        /// <summary>隘口通行宽度（x 方向），隘口两侧为山林减速区。</summary>
        public const float ChokeHalfWidth = 32f;

        private readonly List<TerrainPatch> patches = new();
        private readonly List<MapProp> props = new();

        public SliceMap()
        {
            // 滏口陉隘口两侧山林（减速通行，COMBAT-16）
            patches.Add(new TerrainPatch(new MapPoint(-70f, -75f), 80f, 90f, TerrainClass.Difficult));
            patches.Add(new TerrainPatch(new MapPoint(70f, -75f), 80f, 90f, TerrainClass.Difficult));
            // 野王北侧丘陵
            patches.Add(new TerrainPatch(new MapPoint(-60f, 55f), 90f, 70f, TerrainClass.Difficult));
            patches.Add(new TerrainPatch(new MapPoint(60f, 55f), 90f, 70f, TerrainClass.Difficult));
            // 高都外围山林
            patches.Add(new TerrainPatch(new MapPoint(-75f, 120f), 60f, 90f, TerrainClass.Difficult));
            patches.Add(new TerrainPatch(new MapPoint(75f, 120f), 60f, 90f, TerrainClass.Difficult));

            // 赵：大帐 + 主仓
            props.Add(new MapProp { Id = "zhao_camp", DisplayName = "赵军大帐", Owner = Faction.Zhao, Kind = PropKind.Camp, Position = new MapPoint(0f, -150f), Radius = 18f });
            props.Add(new MapProp { Id = "zhao_granary_main", DisplayName = "赵军主仓", Owner = Faction.Zhao, Kind = PropKind.Granary, Position = new MapPoint(0f, -120f), Radius = 9f, InitialStock = 100f, Stock = 100f });
            props.Add(new MapProp { Id = "zhao_granary_west", DisplayName = "赵军西仓", Owner = Faction.Zhao, Kind = PropKind.Granary, Position = new MapPoint(-55f, -130f), Radius = 9f, InitialStock = 100f, Stock = 100f });

            // 秦：粮道前仓（火攻目标）+ 主仓 + 高都城池
            props.Add(new MapProp { Id = "qin_granary_forward", DisplayName = "秦军粮道前仓", Owner = Faction.Qin, Kind = PropKind.Granary, Position = new MapPoint(0f, 50f), Radius = 9f, InitialStock = 100f, Stock = 100f });
            props.Add(new MapProp { Id = "qin_granary_main", DisplayName = "秦军主仓", Owner = Faction.Qin, Kind = PropKind.Granary, Position = new MapPoint(45f, 120f), Radius = 9f, InitialStock = 100f, Stock = 100f });
            props.Add(new MapProp { Id = "qin_city", DisplayName = "高都", Owner = Faction.Qin, Kind = PropKind.City, Position = new MapPoint(0f, 145f), Radius = 22f });
            props.Add(new MapProp { Id = "qin_camp", DisplayName = "秦军大帐", Owner = Faction.Qin, Kind = PropKind.Camp, Position = new MapPoint(0f, 115f), Radius = 16f });
        }

        public IReadOnlyList<TerrainPatch> Patches => patches;

        public IReadOnlyList<MapProp> Props => props;

        /// <summary>某点的地形（无减速块覆盖则为可通行）。</summary>
        public TerrainClass TerrainAt(MapPoint p)
        {
            foreach (var patch in patches)
            {
                if (System.MathF.Abs(p.X - patch.Center.X) <= patch.Width * 0.5f &&
                    System.MathF.Abs(p.Z - patch.Center.Z) <= patch.Depth * 0.5f)
                {
                    return patch.Terrain;
                }
            }

            return TerrainClass.Passable;
        }

        /// <summary>区域标识（欺骗痕迹 AreaId 用；按走廊分段，与真源"起炊烟区域"概念对应）。</summary>
        public string AreaIdAt(MapPoint p) => p.Z switch
        {
            < -105f => "zhao_rear",
            < -30f => "choke",
            < 65f => "yewang",
            < 105f => "qin_forward",
            _ => "gaodu",
        };

        public IEnumerable<MapProp> GranariesOf(Faction owner)
        {
            foreach (var prop in props)
            {
                if (prop.Kind == PropKind.Granary && prop.Owner == owner)
                {
                    yield return prop;
                }
            }
        }

        /// <summary>地图夹取（含隘口不可通行段：隘口外 x 越界时夹回走廊）。</summary>
        public MapPoint Clamp(MapPoint p)
        {
            var clamped = p.ClampTo(MinX, MaxX, MinZ, MaxZ);
            // 隘口两侧山林外侧为绝壁（不可通行）——用走廊宽度约束，避免绕山。
            if (clamped.Z is > -110f and < -35f)
            {
                float x = System.Math.Clamp(clamped.X, -ChokeHalfWidth, ChokeHalfWidth);
                return new MapPoint(x, clamped.Z);
            }

            return clamped;
        }
    }

}