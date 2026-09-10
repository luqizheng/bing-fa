using ChinaBettle.Foundation.Intel;
using NUnit.Framework;

namespace ChinaBettle.Tests.Foundation;

/// <summary>情报层可信度算子回归（真源 INTEL-01~06；情报规格 §3.1 算例与 §8.3 样例）。</summary>
public sealed class CredibilityCalculatorTests
{
    private static readonly CredibilityConfig C = CredibilityConfig.Default;

    [Test]
    public void Fresh_OneScoutEightMinutes_Is75()
    {
        // 情报规格 §4.1 阶段二：65 + 2×1 + 1×8 = 75
        float fresh = CredibilityCalculator.Fresh(C, IntelSourceType.ScoutVisual, scoutCount: 1, observationMinutes: 8f);
        Assert.That(fresh, Is.EqualTo(75f));
    }

    [Test]
    public void Aged_SampleFromSpec83_Is68()
    {
        // 情报规格 §8.3：fresh=75、base=65、age=2 → floor=19.5、ageFactor≈0.867 → ≈68
        float shown = CredibilityCalculator.Aged(fresh: 75f, baseValue: 65f, ageMinutes: 2f, C);
        Assert.That(shown, Is.EqualTo(68f).Within(1f));
    }

    [Test]
    public void Aged_At15Minutes_ReachesFloorAndExpires()
    {
        // INTEL-04：第 15 分钟恰为基值×30%
        float shown = CredibilityCalculator.Aged(fresh: 90f, baseValue: 80f, ageMinutes: 15f, C);
        Assert.That(shown, Is.EqualTo(24f).Within(0.01f));
    }

    [Test]
    public void Fresh_ScoutBonusCapsAt10Units()
    {
        // INTEL-02：+2%×min(斥候数,10)，上限 +20
        Assert.That(CredibilityCalculator.Fresh(C, IntelSourceType.ScoutVisual, 10, 0f), Is.EqualTo(85f));
        Assert.That(CredibilityCalculator.Fresh(C, IntelSourceType.ScoutVisual, 12, 0f), Is.EqualTo(85f));
    }

    [Test]
    public void Aggregate_WeightsSourcesByTable()
    {
        // INTEL-05：文书 1.2、目视 1.0、口供 0.6。(80×1.2+60×1.0+40×0.6)/2.8 = 68.57
        var items = new (float, IntelSourceType)[]
        {
            (80f, IntelSourceType.CapturedDocument),
            (60f, IntelSourceType.ScoutVisual),
            (40f, IntelSourceType.Prisoner),
        };
        float? agg = CredibilityCalculator.Aggregate(items, C);
        Assert.That(agg, Is.EqualTo(68.57f).Within(0.05f));
    }

    [Test]
    public void Tier_Boundaries_AlignWithDecisionThresholds()
    {
        Assert.That(CredibilityCalculator.Tier(85f, C), Is.EqualTo(CredibilityTier.Confident));
        Assert.That(CredibilityCalculator.Tier(70f, C), Is.EqualTo(CredibilityTier.Trusted));
        Assert.That(CredibilityCalculator.Tier(69f, C), Is.EqualTo(CredibilityTier.Doubtful));
        Assert.That(CredibilityCalculator.Tier(49.9f, C), Is.EqualTo(CredibilityTier.Untrusted));
    }
}
