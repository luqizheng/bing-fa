using ChinaBettle.Foundation.Trust;
using NUnit.Framework;

namespace ChinaBettle.Tests.Foundation;

/// <summary>
/// 幕僚代理信号回归（真源 TRUST-10/TRUST-12；ADV-01/02）。
/// 锁定算例：廉颇（谨慎 9）阈值 +0.12 低估时机；赵括（谨慎 2）阈值 −0.09 高估时机；
/// 低质幕僚按 (100−质量分)×0.5% 概率向性格方向误判一档。
/// </summary>
public sealed class AdvisorSignalTests
{
    private const double NoError = 1.0; // 恒不触发误判的随机源。

    [Test]
    public void LianPo_UnderestimatesTiming_EvenAboveBaseAdvise()
    {
        var signal = new AdvisorSignal(random: static () => NoError);

        // 桶 0.83 已过基准 0.80，但廉颇有效阈值 0.92 → 只给"勉强，恐引复核"（低估时机）。
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.83f), Is.EqualTo(AdvisorAdvice.Hesitant));
        // 桶 0.92 起才放行；0.60 未到廉颇犹豫阈值 0.62 → "先养"。
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.92f), Is.EqualTo(AdvisorAdvice.Advisable));
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.60f), Is.EqualTo(AdvisorAdvice.TooEarly));
    }

    [Test]
    public void ZhaoKuo_OverestimatesTiming_BelowBaseAdvise()
    {
        var signal = new AdvisorSignal(random: static () => NoError);

        // 桶 0.77 未到基准 0.80，但赵括有效阈值 0.71 → 已喊"可施计"（高估时机）。
        Assert.That(signal.Evaluate(AdvisorProfile.ZhaoKuo, 0.77f), Is.EqualTo(AdvisorAdvice.Advisable));
        // 桶 0.55：赵括（犹豫阈值 0.41）→ "勉强"；廉颇（犹豫阈值 0.62）→ "先养"。
        Assert.That(signal.Evaluate(AdvisorProfile.ZhaoKuo, 0.55f), Is.EqualTo(AdvisorAdvice.Hesitant));
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.55f), Is.EqualTo(AdvisorAdvice.TooEarly));
    }

    [Test]
    public void MidBand_MapsToHesitant()
    {
        var signal = new AdvisorSignal(random: static () => NoError);
        Assert.That(signal.Evaluate(AdvisorProfile.ZhaoKuo, 0.65f), Is.EqualTo(AdvisorAdvice.Hesitant));
    }

    [Test]
    public void LowQuality_MisfireShiftsTowardPersonalityBias()
    {
        // 质量分 40 → 误判概率 30%；随机源 0.1 < 0.3 → 必误判。
        var signal = new AdvisorSignal(random: static () => 0.1);

        // 廉颇误判 → 向保守偏一档：可施计 → 勉强。
        var lianpo = AdvisorProfile.LianPo with { QualityScore = 40 };
        Assert.That(signal.Evaluate(lianpo, 0.95f), Is.EqualTo(AdvisorAdvice.Hesitant));

        // 赵括误判 → 向冒进偏一档：先养 → 勉强。
        var zhaokuo = AdvisorProfile.ZhaoKuo with { QualityScore = 40 };
        Assert.That(signal.Evaluate(zhaokuo, 0.30f), Is.EqualTo(AdvisorAdvice.Hesitant));
    }

    [Test]
    public void PerfectQuality_NeverMisfires()
    {
        // 质量分 100 → 误判概率 0；随机源恒 0 也不误判。
        var signal = new AdvisorSignal(random: static () => 0.0);
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.95f), Is.EqualTo(AdvisorAdvice.Advisable));
        Assert.That(signal.Evaluate(AdvisorProfile.ZhaoKuo, 0.30f), Is.EqualTo(AdvisorAdvice.TooEarly));
    }

    [Test]
    public void CustomConfig_ThresholdsHonored()
    {
        var config = new TrustConfig(
            AdvisorAdviseThreshold: 0.60f,
            AdvisorHesitateThreshold: 0.40f,
            AdvisorCautionShiftPerPoint: 0f,
            AdvisorErrorProbabilityPerQualityPoint: 0f);
        var signal = new AdvisorSignal(config, static () => NoError);

        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.60f), Is.EqualTo(AdvisorAdvice.Advisable));
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.45f), Is.EqualTo(AdvisorAdvice.Hesitant));
        Assert.That(signal.Evaluate(AdvisorProfile.LianPo, 0.39f), Is.EqualTo(AdvisorAdvice.TooEarly));
    }
}
