namespace ChinaBettle.Foundation.Trust;

/// <summary>信念层信任桶全部可调参数（真源 TRUST-01~08）。默认值=v1.1 真源表。</summary>
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
    float DetectedValue = 0.20f)
{
    public static TrustConfig Default { get; } = new();
}
