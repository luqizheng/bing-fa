
namespace ChinaBettle.Foundation.Units
{
    /// <summary>
    /// 兵种克制环与系数（真源 COMBAT-01/COMBAT-02）：
    /// 枪兵 → 轻骑 → 弩兵 → 重步 → 枪兵；克制 ×1.35、被克 ×0.75、无环上关系 ×1.00。
    /// </summary>
    public sealed class CounterRing
    {
        private readonly CombatConfig config;

        public CounterRing(CombatConfig? config = null)
        {
            this.config = config ?? CombatConfig.Default;
        }

        public float Coefficient(BaseUnitClass attacker, BaseUnitClass defender)
        {
            if (Counters(attacker, defender))
            {
                return config.CounterMultiplier;
            }

            if (Counters(defender, attacker))
            {
                return config.CounteredMultiplier;
            }

            return config.NeutralMultiplier;
        }

        /// <summary>环上克制关系：枪→骑→弩→重步→枪。</summary>
        public static bool Counters(BaseUnitClass attacker, BaseUnitClass defender) =>
            (attacker, defender) switch
            {
                (BaseUnitClass.Spear, BaseUnitClass.LightCavalry) => true,
                (BaseUnitClass.LightCavalry, BaseUnitClass.Crossbow) => true,
                (BaseUnitClass.Crossbow, BaseUnitClass.HeavyInfantry) => true,
                (BaseUnitClass.HeavyInfantry, BaseUnitClass.Spear) => true,
                _ => false,
            };
    }

}