using System.Collections.Generic;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    // 说明（P2 数据化，2026-09-11）：本文件原含 TerrainPatch 与 SliceMap（硬编码的滏口陉地图）。
    // 地图已迁移为数据驱动（见 BattleMapDefinition / SliceMaps），SliceMap 与 TerrainPatch 已删除。
    // 本文件现仅承载地图物件类型：PropKind 与 MapProp（被 BattleMapDefinition 与表现层共用）。

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

}
