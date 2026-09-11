using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Units
{
    /// <summary>
    /// 单位战斗属性（GDD §2.5.1；切片数值见《基础兵种数值表 v0.2》COMBAT-14）。
    /// 数据驱动：具体数值从配置资产注入，本结构不设硬编码默认战斗数值。
    /// 1 个单位 = 100 人/队（COMBAT-13，与 FOOD-02 口粮口径一致）。
    /// </summary>
    public sealed record UnitStats(
        BaseUnitClass Class,
        float MaxHealth,
        float Attack,
        float Defense,
        float MoveSpeed,
        float AttackRange,
        float AttackIntervalSeconds,
        int Cost,
        float TrainSeconds)
    {
        /// <summary>国家级强化/变体的属性修正（COMBAT-03：+15%~30% 区间，具体走配置）。</summary>
        public UnitStats ApplyEliteMultiplier(float multiplier) => this with
        {
            MaxHealth = MaxHealth * multiplier,
            Attack = Attack * multiplier,
            Defense = Defense * multiplier,
        };
    }

}