
namespace ChinaBettle.Foundation.Units
{
    /// <summary>战斗/兵种/地形全部可调参数（真源 COMBAT-02/08/09/11/12/13/15/16/17；SKILL-03 火区灼烧与火区持续）。默认值=真源表初值。</summary>
    public sealed record CombatConfig(
        float CounterMultiplier = 1.35f,
        float CounteredMultiplier = 0.75f,
        float NeutralMultiplier = 1.00f,
        int MinDamage = 1,
        float MoraleHighAttackModifier = 0.10f,
        float MoraleHighMoveModifier = 0.05f,
        float MoraleShakenAttackModifier = -0.20f,
        float MoraleShakenMoveModifier = -0.10f,
        float MoraleShakenCoefficient = 0.8f,
        float DifficultTerrainSpeedMultiplier = 0.6f,
        float QinRankAttackBonusPerKill = 0.01f,
        float QinRankAttackBonusCap = 0.20f,
        float SpearFormationMovePenalty = 0.20f,
        int CavalryReflectDamagePerHit = 6,
        int FireZoneBurnDamagePerTick = 8,
        float FireZoneBurnIntervalSeconds = 2f,
        float FireZoneDurationSeconds = 60f,
        float EliteMinBonus = 0.15f,
        float EliteMaxBonus = 0.30f,
        int UnitMenCount = 100)
    {
        public static CombatConfig Default { get; } = new();
    }

}