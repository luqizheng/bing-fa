
namespace ChinaBettle.Foundation.Map
{
    /// <summary>
    /// 三级地形通行模型（真源 COMBAT-05）。
    /// </summary>
    public enum TerrainClass
    {
        /// <summary>可通行：平原、官道。</summary>
        Passable,

        /// <summary>减速通行：山林、沼泽、丘陵（非赵骑移速 ×0.6，COMBAT-16）。</summary>
        Difficult,

        /// <summary>不可通行：绝壁、深水、密集山林（仅"暗度陈仓"可限时改一段为 Difficult，COMBAT-07）。</summary>
        Impassable,
    }

}