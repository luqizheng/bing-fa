
namespace ChinaBettle.Foundation.Supply
{
    /// <summary>补给与饥饿参数（真源 FOOD-01/02/04/05、TIME-03）。默认值=真源表初值。</summary>
    public sealed record SupplyConfig(
        int MaxRationDays = 3,
        int BattleMinutesPerNarrativeDay = 6,
        int RationTickSeconds = 60,
        float StarvationGainPerTick = 0.10f,
        float StarvationAttackPenaltyThreshold = 0.50f,
        float StarvationAttackPenalty = -0.20f,
        float StarvationMovePenalty = -0.30f,
        float MoraleDamageThreshold = 0.80f,
        int MoraleDamagePerTick = -10,
        int BurnGranaryStarvationJump = 30,
        int FireAttackRationLossPercent = 40,
        int FireZoneDurationSeconds = 60)
    {
        /// <summary>携粮上限折算的战场分钟数（FOOD-01：3 日粮 = 18 战场分钟）。</summary>
        public int MaxRationBattleMinutes => MaxRationDays * BattleMinutesPerNarrativeDay;

        public static SupplyConfig Default { get; } = new();
    }

}