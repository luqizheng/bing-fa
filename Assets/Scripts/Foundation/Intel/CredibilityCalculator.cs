using System;

namespace ChinaBettle.Foundation.Intel;

/// <summary>
/// 情报层可信度算子（情报规格 §3.1/§3.2；真源 INTEL-01~06）。
/// 纯逻辑、无 Unity 依赖。所有阈值/基值从 <see cref="CredibilityConfig"/> 注入，禁止硬编码（DECP-01 数据驱动原则）。
/// </summary>
public static class CredibilityCalculator
{
    /// <summary>
    /// 新鲜可信度（情报刚产生时）。
    /// fresh = min(100, 基值 + 2%×min(斥候数,10) + 1%×min(连续目击分钟数,20))（INTEL-02/03）。
    /// 注意：敌方欺骗技能不进入本公式（只篡改载荷，DECP-01）。
    /// </summary>
    public static float Fresh(CredibilityConfig config, IntelSourceType source, int scoutCount, float observationMinutes)
    {
        float baseValue = SourceBase(config, source, historicalConsistency: 0.5f);
        float scoutBonus = config.ScoutCountBonusPerUnit * MathF.Min(scoutCount, config.ScoutCountCap);
        float durationBonus = config.ObservationBonusPerMinute * MathF.Min(observationMinutes, config.ObservationMinuteCap);
        return MathF.Min(100f, baseValue + scoutBonus + durationBonus);
    }

    /// <summary>
    /// 规律归纳来源的新鲜可信度：基值在生成时按历史一致率快照并 clamp（INTEL-10）。
    /// </summary>
    public static float FreshPatternInference(CredibilityConfig config, float historicalConsistency, float observationMinutes)
    {
        float clampedConsistency = Math.Clamp(historicalConsistency, config.PatternBaseMin, config.PatternBaseMax);
        float durationBonus = config.ObservationBonusPerMinute * MathF.Min(observationMinutes, config.ObservationMinuteCap);
        return MathF.Min(100f, clampedConsistency + durationBonus);
    }

    /// <summary>
    /// 年龄衰减后的当前可信度（INTEL-04）：
    /// floor = 基值×30%；ageFactor = max(0, 1 − age/15)；shown = floor + (fresh−floor)×ageFactor。
    /// age ≥ 15 分钟调用方应将状态置为 Expired，本方法在该区间返回 floor。
    /// </summary>
    public static float Aged(float fresh, float baseValue, float ageMinutes, CredibilityConfig config)
    {
        float floor = baseValue * config.AgeFloorRatio;
        float ageFactor = MathF.Max(0f, 1f - ageMinutes / config.ExpiryMinutes);
        return floor + (fresh - floor) * ageFactor;
    }

    /// <summary>
    /// 综合可信度（INTEL-05）：同主题未过期、未标记伪造情报按来源权重加权平均。
    /// 输入为 (当前可信度, 来源) 元组列表；调用方负责过滤 Active 状态。
    /// </summary>
    public static float? Aggregate(ReadOnlySpan<(float Credibility, IntelSourceType Source)> activeIntel, CredibilityConfig config)
    {
        if (activeIntel.IsEmpty)
        {
            return null;
        }

        float weightedSum = 0f;
        float weightSum = 0f;
        foreach (var (credibility, source) in activeIntel)
        {
            float weight = SourceWeight(config, source);
            weightedSum += credibility * weight;
            weightSum += weight;
        }

        return weightedSum / weightSum;
    }

    /// <summary>情报层分档（INTEL-06）。玩家 UI 与 AI 决策共用。</summary>
    public static CredibilityTier Tier(float aggregateCredibility, CredibilityConfig config)
    {
        if (aggregateCredibility >= config.ConfidentThreshold)
        {
            return CredibilityTier.Confident;
        }

        if (aggregateCredibility >= config.TrustedThreshold)
        {
            return CredibilityTier.Trusted;
        }

        if (aggregateCredibility >= config.DoubtfulThreshold)
        {
            return CredibilityTier.Doubtful;
        }

        return CredibilityTier.Untrusted;
    }

    /// <summary>来源基值（INTEL-01）。PatternInference 需传入历史一致率。</summary>
    public static float SourceBase(CredibilityConfig config, IntelSourceType source, float historicalConsistency) => source switch
    {
        IntelSourceType.ScoutVisual => config.ScoutVisualBase,
        IntelSourceType.Prisoner => config.PrisonerBase,
        IntelSourceType.CapturedDocument => config.CapturedDocumentBase,
        IntelSourceType.SkillReveal => config.SkillRevealBase,
        IntelSourceType.PatternInference => Math.Clamp(historicalConsistency, config.PatternBaseMin, config.PatternBaseMax),
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    /// <summary>来源综合权重（INTEL-05）。</summary>
    public static float SourceWeight(CredibilityConfig config, IntelSourceType source) => source switch
    {
        IntelSourceType.ScoutVisual => config.ScoutVisualWeight,
        IntelSourceType.Prisoner => config.PrisonerWeight,
        IntelSourceType.CapturedDocument => config.CapturedDocumentWeight,
        IntelSourceType.SkillReveal => config.SkillRevealWeight,
        IntelSourceType.PatternInference => config.PatternInferenceWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}

/// <summary>情报层分档（INTEL-06）。</summary>
public enum CredibilityTier
{
    /// <summary>≥85% 确信（叙事大事件还需信任桶 &gt;0.8 双高，TRUST-08）。</summary>
    Confident,

    /// <summary>70–84% 可信（信任桶 &gt;0.8 时可决策）。</summary>
    Trusted,

    /// <summary>50–69% 存疑，强制复核（TRUST-07）。</summary>
    Doubtful,

    /// <summary>&lt;50% 不可信，不据此决策。</summary>
    Untrusted,
}
