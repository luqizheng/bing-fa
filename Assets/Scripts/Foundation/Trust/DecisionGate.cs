using ChinaBettle.Foundation.Intel;

namespace ChinaBettle.Foundation.Trust
{
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
        /// <summary>
        /// 门控评估（真源 TRUST-05）。
        ///
        /// **档位口径（v1.4 修正）**：<paramref name="tier"/> 应取"**聚合可信度与最新单条中更可信者**"的档位，
        /// 而不是只取聚合。原因（实机发现）：`INTEL-05` 的综合是全活跃条目的加权平均，
        /// 早期条目按 `INTEL-04` 衰减到地板后会**拖低均值**——同一主题侦查越久、综合反而越低
        /// （实测：单条 fresh 72.3% 但综合仅 63–67%），导致 AI 永远落入存疑档、恒走强复核，
        /// 既不采信载荷（欺骗失效）也无法行动（态势冻结）。
        /// TRUST-09 的语义是桶衡量"对**当前载荷**的采信度"，而当前载荷正是最新一条——
        /// 故取更可信者才与之相符。调用方见 <c>IntelDrivenDecision.OnIntel</c>。
        /// </summary>
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

}