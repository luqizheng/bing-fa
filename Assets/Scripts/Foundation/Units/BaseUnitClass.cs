
namespace ChinaBettle.Foundation.Units
{
    /// <summary>
    /// 基础兵种 4 类，承载克制环（真源 COMBAT-01/COMBAT-04）。
    /// 七国特色兵种是某基础类的国家级强化/变体，继承全部克制关系，不新增第五类。
    /// </summary>
    public enum BaseUnitClass
    {
        /// <summary>枪兵/矛阵（魏武卒、齐技击之士为其变体）。</summary>
        Spear,

        /// <summary>轻骑（赵胡服骑射、燕辽东铁骑）。</summary>
        LightCavalry,

        /// <summary>弩兵/弓兵（韩劲弩、楚巨弩楼船水域变体）。</summary>
        Crossbow,

        /// <summary>重步兵（秦锐士）。</summary>
        HeavyInfantry,
    }

    /// <summary>阵营（MVP 仅赵、秦，真源 SLICE-01）。</summary>
    public enum Faction
    {
        Zhao,
        Qin,

        // 第二阶段扩展（GDD §2.4.2 映射表），MVP 不得启用。
        Chu,
        Qi,
        Wei,
        Han,
        Yan,
    }

}