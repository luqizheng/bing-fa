using System.Collections.Generic;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 地形体块（矩形、不重叠）：一个通行等级覆盖区（真源 COMBAT-05/16）。
    /// 与旧 <c>TerrainPatch</c> 语义一致，改名是为与"地图定义"术语对齐（P2 数据化）。
    /// </summary>
    public sealed record TerrainVolume(MapPoint Center, float Width, float Depth, TerrainClass Terrain);

    /// <summary>
    /// 壁垒线（工事，真源 COMBAT-18）：一条沿 Z 轴延伸、固定 X 的纵向工事。
    ///
    /// 规则落地：①非缺口段 = 不可通行（全单位，含赵骑——COMBAT-06 的"忽略减速"只对
    /// Difficult 生效，不适用于 Impassable）；②缺口/关隘段（<see cref="GateSpans"/>）可通行；
    /// ③守方（<see cref="Owner"/>）在缺口段防御力 ×(1 + <see cref="DefenderDefenseBonus"/>)。
    ///
    /// 缺口按 Z 区间表示，可有多段（例如壁垒被河流切断形成两处通路）。
    /// </summary>
    public sealed record FortificationLine(
        string Id,
        string DisplayName,
        Faction Owner,
        float FixedX,
        float MinZ,
        float MaxZ,
        IReadOnlyList<(float MinZ, float MaxZ)> GateSpans,
        float DefenderDefenseBonus)
    {
        /// <summary>该点是否落在壁垒线上（含缺口与非缺口段）。</summary>
        public bool Contains(float x, float z, float thickness) =>
            System.MathF.Abs(x - FixedX) <= thickness * 0.5f && z >= MinZ && z <= MaxZ;

        /// <summary>该点是否落在缺口段（唯一可通行处）。</summary>
        public bool IsGateAt(float z)
        {
            foreach (var gate in GateSpans)
            {
                if (z >= gate.MinZ && z <= gate.MaxZ)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>初始部署条目（真源 CP-05）：单位模板 + 出生点 + 展示标签。</summary>
    public sealed record MapDeployment(UnitDefinition Definition, MapPoint Position, string Label);

    /// <summary>
    /// 地图分区（用于欺骗痕迹的 AreaId 与"区域"文案）：沿某轴切分的区间。
    /// 分区轴按地图对峙方向选择——南北对峙取 Z 轴，东西对峙取 X 轴。
    /// </summary>
    public sealed record MapArea(string Id, string DisplayName, float From, float To, bool AlongX);

    /// <summary>
    /// 战役地图定义（数据驱动，替代硬编码的 <c>SliceMap</c>）。
    ///
    /// 纪律：本类只承载**地图内容数据**（几何/点位/部署），不承载真源数值——通行等级与系数
    /// 引用真源 COMBAT-05/16/18，几何尺寸引用真源 MAP-xx（见各图定义处的 ID 注释）。
    /// 纯逻辑、无 UnityEngine 依赖，可被权威服务器复用。
    /// </summary>
    public sealed class BattleMapDefinition
    {
        /// <summary>壁垒线判定厚度（米）：线宽，非真源值，实现用常量。</summary>
        public const float FortificationThickness = 6f;

        private TerrainVolume[]? volumesByArea;

        /// <summary>体块按面积升序（小体块＝通行口，优先于包含它的大体块）。</summary>
        private TerrainVolume[] VolumesByArea
        {
            get
            {
                if (volumesByArea is null)
                {
                    var sorted = new TerrainVolume[Volumes.Count];
                    for (int i = 0; i < Volumes.Count; i++)
                    {
                        sorted[i] = Volumes[i];
                    }

                    System.Array.Sort(sorted, (a, b) =>
                        (a.Width * a.Depth).CompareTo(b.Width * b.Depth));
                    volumesByArea = sorted;
                }

                return volumesByArea;
            }
        }

        public BattleMapDefinition(
            string id,
            string displayName,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            IReadOnlyList<TerrainVolume> volumes,
            IReadOnlyList<MapProp> props,
            IReadOnlyList<FortificationLine> fortifications,
            IReadOnlyList<MapArea> areas,
            IReadOnlyList<MapDeployment> deployments,
            MapPoint playerBase,
            MapPoint aiBase,
            MapPoint aiHoldLine)
        {
            Id = id;
            DisplayName = displayName;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            Volumes = volumes;
            Props = props;
            Fortifications = fortifications;
            Areas = areas;
            Deployments = deployments;
            PlayerBase = playerBase;
            AiBase = aiBase;
            AiHoldLine = aiHoldLine;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public float MinX { get; }

        public float MaxX { get; }

        public float MinZ { get; }

        public float MaxZ { get; }

        public IReadOnlyList<TerrainVolume> Volumes { get; }

        public IReadOnlyList<MapProp> Props { get; }

        public IReadOnlyList<FortificationLine> Fortifications { get; }

        public IReadOnlyList<MapArea> Areas { get; }

        public IReadOnlyList<MapDeployment> Deployments { get; }

        /// <summary>玩家（赵）本阵坐标：溃逃归队点与表现层取景中心。</summary>
        public MapPoint PlayerBase { get; }

        /// <summary>AI（秦）本阵坐标。</summary>
        public MapPoint AiBase { get; }

        /// <summary>AI 守势线坐标（未采信"敌弱"时的依托位置）。</summary>
        public MapPoint AiHoldLine { get; }

        public float Width => MaxX - MinX;

        public float Depth => MaxZ - MinZ;

        // ───────────────────────────── 查询 ─────────────────────────────

        /// <summary>
        /// 某点地形（真源 COMBAT-05/16/18）：先判壁垒线（不可通行或缺口守方的可通行），
        /// 再判地形体块，最后回落到可通行。
        /// </summary>
        public TerrainClass TerrainAt(MapPoint p)
        {
            foreach (var line in Fortifications)
            {
                if (line.Contains(p.X, p.Z, FortificationThickness))
                {
                    // 缺口段可通行（守方在此享防御加成，走 DefenseBonusAt）；
                    // 非缺口段不可通行（COMBAT-18①，赵骑忽略减速不适用）。
                    return line.IsGateAt(p.Z) ? TerrainClass.Passable : TerrainClass.Impassable;
                }
            }

            // 体块按面积升序扫描：**允许"小体块嵌套在大体块内"以在不可通行区域上开通行口**
            // （渡口即河流体块内的可通行小体块、壁垒缺口同理由 FortificationLine.GateSpans 表达）。
            foreach (var volume in VolumesByArea)
            {
                if (System.MathF.Abs(p.X - volume.Center.X) <= volume.Width * 0.5f &&
                    System.MathF.Abs(p.Z - volume.Center.Z) <= volume.Depth * 0.5f)
                {
                    return volume.Terrain;
                }
            }

            return TerrainClass.Passable;
        }

        /// <summary>
        /// 壁垒缺口段的守方防御加成（真源 COMBAT-18③）：守方单位位于**己方**壁垒缺口段时返回加成，
        /// 否则 0。攻方不享受。
        /// </summary>
        public float DefenderDefenseBonusAt(MapPoint p, Faction defender)
        {
            foreach (var line in Fortifications)
            {
                if (line.Owner != defender)
                {
                    continue;
                }

                if (line.Contains(p.X, p.Z, FortificationThickness) && line.IsGateAt(p.Z))
                {
                    return line.DefenderDefenseBonus;
                }
            }

            return 0f;
        }

        /// <summary>区域标识（欺骗痕迹 AreaId 与区域文案）。</summary>
        public string AreaIdAt(MapPoint p)
        {
            foreach (var area in Areas)
            {
                float value = area.AlongX ? p.X : p.Z;
                if (value >= area.From && value < area.To)
                {
                    return area.Id;
                }
            }

            return Areas.Count > 0 ? Areas[Areas.Count - 1].Id : "unknown";
        }

        /// <summary>区域展示名（编年史文案用）。</summary>
        public string AreaDisplayName(string areaId)
        {
            foreach (var area in Areas)
            {
                if (area.Id == areaId)
                {
                    return area.DisplayName;
                }
            }

            return areaId;
        }

        /// <summary>
        /// 边界与不可通行地形夹取（COMBAT-05/18）。
        /// 单位不可进入不可通行格：若目标点不可通行，则保留当前坐标在该轴上的分量。
        /// 缺口与体块均为轴对齐矩形，故逐轴处理即可保证不穿墙。
        /// </summary>
        public MapPoint Clamp(MapPoint from, MapPoint to)
        {
            var bounded = to.ClampTo(MinX, MaxX, MinZ, MaxZ);
            float x = bounded.X;
            float z = bounded.Z;

            if (TerrainAt(new MapPoint(x, from.Z)) == TerrainClass.Impassable)
            {
                x = from.X;
            }

            if (TerrainAt(new MapPoint(x, z)) == TerrainClass.Impassable)
            {
                z = from.Z;
            }

            return new MapPoint(x, z);
        }

        /// <summary>边界夹取（不做通行性判定；用于初始部署等已知合法点）。</summary>
        public MapPoint ClampToBounds(MapPoint p) => p.ClampTo(MinX, MaxX, MinZ, MaxZ);

        public IEnumerable<MapProp> GranariesOf(Faction owner)
        {
            foreach (var prop in Props)
            {
                if (prop.Kind == PropKind.Granary && prop.Owner == owner)
                {
                    yield return prop;
                }
            }
        }

        /// <summary>据点（大帐/城池）——夺取判定的目标集合（GDD §2.7.1）。</summary>
        public IEnumerable<MapProp> Strongholds()
        {
            foreach (var prop in Props)
            {
                if (prop.Kind is PropKind.Camp or PropKind.City)
                {
                    yield return prop;
                }
            }
        }

        /// <summary>取指定阵营的本阵（大帐）。</summary>
        public MapProp? CampOf(Faction owner)
        {
            foreach (var prop in Props)
            {
                if (prop.Kind == PropKind.Camp && prop.Owner == owner)
                {
                    return prop;
                }
            }

            return null;
        }

        /// <summary>本阵坐标（溃逃归队点）：优先取大帐，无大帐时回落基准点。</summary>
        public MapPoint BaseOf(Faction faction) =>
            CampOf(faction)?.Position ?? (faction == Faction.Zhao ? PlayerBase : AiBase);
    }

}
