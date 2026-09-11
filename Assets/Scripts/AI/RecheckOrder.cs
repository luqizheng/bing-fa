
namespace ChinaBettle.AI
{
    /// <summary>
    /// 一次强制复核任务（情报规格 §5.3）。纯逻辑计时：30–60 战场秒；暂停由战役时钟冻结承载（TIME-02）。
    /// </summary>
    public sealed class RecheckOrder
    {
        public string Topic { get; init; }

        public RecheckState Kind { get; init; }

        public float StartSeconds { get; init; }

        public float DurationSeconds { get; init; }

        public int ScoutsToDispatch { get; init; }

        public float EndSeconds => StartSeconds + DurationSeconds;

        public bool IsOngoingAt(float nowSeconds) => nowSeconds < EndSeconds;

        /// <summary>强复核期间冻结高利害行动；弱复核只冻结新决策（由行为树按 Kind 解释）。</summary>
        public bool FreezesAllHighStakesActions => Kind == RecheckState.DoubtfulStrong;
    }

}