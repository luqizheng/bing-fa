using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Trust;

namespace ChinaBettle.AI
{
    /// <summary>
    /// 对手 AI 决策主链路的情报门控（真源 AI-01/SLICE-04）：
    /// 情报模块 → 信任桶 → 联合门控 → 行为树/Utility AI；ML-Agents 不进决策主链路。
    /// 本类把【情报层档位】与【信念层桶值】确定性地映射为门控结果，供行为树消费。
    /// </summary>
    public sealed class IntelDrivenDecision
    {
        private readonly TrustBucketSystem trust;
        private readonly TrustConfig trustConfig;
        private readonly CredibilityConfig credibilityConfig;

        public IntelDrivenDecision(
            TrustBucketSystem? trust = null,
            TrustConfig? trustConfig = null,
            CredibilityConfig? credibilityConfig = null)
        {
            this.trustConfig = trustConfig ?? TrustConfig.Default;
            this.credibilityConfig = credibilityConfig ?? CredibilityConfig.Default;
            this.trust = trust ?? new TrustBucketSystem(this.trustConfig);
        }

        public TrustBucketSystem Trust => trust;

        /// <summary>
        /// 吸收一条新情报：增量更新桶（TRUST-02）。返回更新后的桶值与联合门控。
        /// flagged_fake 由 <see cref="MarkDeceptionDetected"/> 单独处置（TRUST-06 优先于增量更新）。
        /// </summary>
        public (float TrustValue, DecisionGate Gate) OnIntel(
            string topic,
            float credibilityPercent,
            int distinctSourceKinds,
            float aggregateCredibility,
            float battleClockSeconds)
        {
            float independence = TrustBucketSystem.IndependenceFactor(distinctSourceKinds, trustConfig);
            float trustValue = trust.Update(topic, credibilityPercent, independence, battleClockSeconds);

            // 档位（v1.4 修正）：取【聚合】与【最新单条 credibilityPercent】中更可信者的档位。
            // 背景：聚合是加权平均，早期条目经 INTEL-04 衰减到地板后会拖低均值，
            // 造成"侦查越久综合越低"（实测 63–67% 恒存疑），使 AI 永远强复核、永不采信。
            // TRUST-09 的桶衡量的是"对当前载荷的采信度"，当前载荷＝最新一条，故取更可信者。
            float effectiveCredibility = System.Math.Max(aggregateCredibility, credibilityPercent);
            var tier = CredibilityCalculator.Tier(effectiveCredibility, credibilityConfig);
            var gate = DecisionGateEvaluator.Evaluate(tier, trustValue, trustConfig, credibilityConfig);
            return (trustValue, gate);
        }

        /// <summary>识破处置（TRUST-06/09）：桶直接置 0.20，进入识破型【弱】复核（在途命令保持惯性）。</summary>
        public RecheckState MarkDeceptionDetected(string topic, float battleClockSeconds)
        {
            trust.SetDetected(topic, battleClockSeconds);
            return RecheckState.PostDetectionWeak;
        }

        /// <summary>存疑型【强】复核触发判定（TRUST-07）：门控为 ForceRecheck 即触发（桶 &lt;0.3 无视档位）。</summary>
        public static bool RequiresStrongRecheck(DecisionGate gate) => gate == DecisionGate.ForceRecheck;
    }

    /// <summary>两类强制复核（情报规格 §5.3；TRUST-07 存疑型强复核 / TRUST-09 识破型弱复核）。</summary>
    public enum RecheckState
    {
        None,

        /// <summary>强复核：部署 2–3 斥候、30–60 战场秒，期间冻结全部高利害行动（不进攻/不撤退/不调兵）。</summary>
        DoubtfulStrong,

        /// <summary>弱复核：在途命令保持惯性（行军/冲锋继续），只冻结新的高利害决策。</summary>
        PostDetectionWeak,
    }

}