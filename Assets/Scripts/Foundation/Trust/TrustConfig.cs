namespace ChinaBettle.Foundation.Trust;

/// <summary>信念层信任桶全部可调参数（真源 TRUST-01~11）。默认值=v1.2 真源表。</summary>
public sealed record TrustConfig(
    float InitialValue = 0.50f,
    float MinValue = 0.10f,
    float MaxValue = 0.95f,
    float UpdateStep = 0.30f,
    float IndependenceOneType = 0.6f,
    float IndependenceTwoTypes = 0.8f,
    float IndependenceThreeTypes = 1.0f,
    float RecentWindowMinutes = 2f,
    float TimeFactorRecent = 1.0f,
    float TimeFactorStale = 0.5f,
    float HighlySuspiciousThreshold = 0.30f,
    float TrustedThreshold = 0.80f,
    float DetectedValue = 0.20f,
    float ReflectionLossRatio = 0.30f,
    float ReflectionPenaltyMultiplier = 0.50f,

    // TRUST-10 幕僚代理信号基准阈值：桶 >0.80 "可施计"；0.50–0.80 "勉强，恐引复核"；<0.50 "时机未到，先养"。
    float AdvisorAdviseThreshold = 0.80f,
    float AdvisorHesitateThreshold = 0.50f,

    // TRUST-12【暂定初值】性格偏移（ADV-01）：有效阈值 = 基准 + (谨慎度−5)×本系数。
    // 廉颇（谨慎 9）→ 阈值 +0.12，低估时机；赵括（谨慎 2）→ 阈值 −0.09，高估时机。
    float AdvisorCautionShiftPerPoint = 0.03f,

    // TRUST-12【暂定初值】低质误判概率（ADV-02）：误判概率 = (100−质量分)×本系数；质量分 100 = 零误判。
    float AdvisorErrorProbabilityPerQualityPoint = 0.005f)
{
    public static TrustConfig Default { get; } = new();
}
