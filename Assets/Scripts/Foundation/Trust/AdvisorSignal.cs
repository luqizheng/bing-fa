using System;

namespace ChinaBettle.Foundation.Trust
{
    /// <summary>幕僚"施计时机"建议三档（真源 TRUST-10）。</summary>
    public enum AdvisorAdvice
    {
        /// <summary>"可施计"。</summary>
        Advisable,

        /// <summary>"勉强，恐引复核"。</summary>
        Hesitant,

        /// <summary>"时机未到，先养"。</summary>
        TooEarly,
    }

    /// <summary>
    /// 幕僚性格与质量画像（真源 ADV-01/02）。
    /// 谨慎度为 ADV-01 五维之一（0–9，终身固定）；质量分为 ADV-02 隐藏分（0–100，按期望收益评估）。
    /// </summary>
    public sealed record AdvisorProfile(string Name, int Caution, int QualityScore)
    {
        /// <summary>廉颇（ADV-01：谨慎 9）。</summary>
        public static readonly AdvisorProfile LianPo = new("廉颇", Caution: 9, QualityScore: 100);

        /// <summary>赵括（ADV-01：谨慎 2）。</summary>
        public static readonly AdvisorProfile ZhaoKuo = new("赵括", Caution: 2, QualityScore: 100);
    }

    /// <summary>
    /// 幕僚代理信号（真源 TRUST-10）：按 AI 信任桶给玩家"施计时机"建议，
    /// **只返回档位，不暴露桶值**——桶仍是 AI 内部状态（评审阻塞项 1 的落地机制）。
    ///
    /// 判定规则（TRUST-10/TRUST-12）：
    /// - 基准阈值：桶 &gt;0.80 → 可施计；0.50–0.80 → 勉强；&lt;0.50 → 先养。
    /// - 性格偏移（ADV-01）：有效阈值 = 基准 + (谨慎度−5)×偏移系数——
    ///   谨慎幕僚低估时机（要求更高桶才建议施计），激进幕僚高估时机。
    /// - 低质误判（ADV-02）：误判概率 = (100−质量分)×每点概率；误判时建议向性格倾向方向偏一档
    ///   （低质幕僚放大性格偏差：谨慎者更保守、激进者更冒进）。
    /// 纯逻辑、无 Unity 依赖；随机源注入以便测试与回放（编年史 §5.4）。
    /// </summary>
    public sealed class AdvisorSignal
    {
    private readonly TrustConfig config;
    private readonly Func<double> random;

    /// <summary>默认随机源（Unity 的 .NET Standard 2.1 没有 Random.Shared，故自持实例）。</summary>
    private static readonly Random SharedRandom = new Random();

    public AdvisorSignal(TrustConfig? config = null, Func<double>? random = null)
    {
        this.config = config ?? TrustConfig.Default;
        this.random = random ?? new Func<double>(() => SharedRandom.NextDouble());
    }

        /// <summary>按桶值产出建议档位。调用方传入 AI 信任桶快照值，本方法不读、不暴露桶本身。</summary>
        public AdvisorAdvice Evaluate(AdvisorProfile advisor, float bucketValue)
        {
            float shift = (advisor.Caution - 5) * config.AdvisorCautionShiftPerPoint;
            float adviseThreshold = Math.Clamp(config.AdvisorAdviseThreshold + shift, 0f, 1f);
            float hesitateThreshold = Math.Clamp(config.AdvisorHesitateThreshold + shift, 0f, 1f);

            var advice = bucketValue >= adviseThreshold ? AdvisorAdvice.Advisable
                       : bucketValue >= hesitateThreshold ? AdvisorAdvice.Hesitant
                       : AdvisorAdvice.TooEarly;

            float errorProbability = Math.Clamp(
                (100 - advisor.QualityScore) * config.AdvisorErrorProbabilityPerQualityPoint, 0f, 1f);
            if (random() < errorProbability)
            {
                advice = ShiftTowardBias(advice, advisor.Caution);
            }

            return advice;
        }

        /// <summary>向性格倾向方向偏一档：谨慎（≥5）更保守，激进（&lt;5）更冒进；已在端点则不动。</summary>
        private AdvisorAdvice ShiftTowardBias(AdvisorAdvice advice, int caution)
        {
            int step = caution >= 5 ? +1 : -1; // 枚举序 Advisable(0) → Hesitant(1) → TooEarly(2)，+1 即更保守。
            int shifted = Math.Clamp((int)advice + step, (int)AdvisorAdvice.Advisable, (int)AdvisorAdvice.TooEarly);
            return (AdvisorAdvice)shifted;
        }
    }

}