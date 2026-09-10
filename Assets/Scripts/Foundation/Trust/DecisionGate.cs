namespace ChinaBettle.Foundation.Trust;

using ChinaBettle.Foundation.Intel;

/// <summary>
/// 联合门控结果（情报规格 §5.2/§5.3；真源 TRUST-05/07/08）。
/// </summary>
public enum DecisionGate
{
    /// <summary>桶 &gt;0.8 且情报达可信/确信档：可直接决策。</summary>
    Act,

    /// <summary>综合可信度 ≥85% 且桶 &gt;0.8：可触发叙事大事件（TRUST-08 双高）。</summary>
    Confident,

    /// <summary>存疑型强复核（TRUST-07）：50–69%，或可信档撞桶 ≤0.8，或桶 &lt;0.3；冻结全部高利害行动。</summary>
    ForceRecheck,

    /// <summary>&lt;50%：不采信，不据此决策。</summary>
    Reject,
}

/// <summary>情报层档位 × 信念层信任桶的确定性联合门控（无随机决策，GDD §5.2 v1.1）。</summary>
public static class DecisionGateEvaluator
{
    public static DecisionGate Evaluate(
        CredibilityTier tier,
        float trustBucketValue,
        TrustConfig? trustConfig = null,
        CredibilityConfig? credibilityConfig = null)
    {
        var tc = trustConfig ?? TrustConfig.Default;
        var cc = credibilityConfig ?? CredibilityConfig.Default;

        if (tier == CredibilityTier.Untrusted)
        {
            return DecisionGate.Reject;
        }

        // 高度怀疑桶：无视单条情报档位，一律强制复核（TRUST-05）。
        if (trustBucketValue < tc.HighlySuspiciousThreshold)
        {
            return DecisionGate.ForceRecheck;
        }

        bool trusted = trustBucketValue > tc.TrustedThreshold;

        if (tier == CredibilityTier.Confident && trusted)
        {
            return DecisionGate.Confident;
        }

        if ((tier == CredibilityTier.Confident || tier == CredibilityTier.Trusted) && trusted)
        {
            return DecisionGate.Act;
        }

        // 50–69% 存疑，或情报达可信档但信任桶未过线 → 存疑型强制复核。
        return DecisionGate.ForceRecheck;
    }
}
