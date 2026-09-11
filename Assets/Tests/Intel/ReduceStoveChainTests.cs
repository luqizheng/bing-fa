using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Stratagems;
using ChinaBettle.Foundation.Trust;
using ChinaBettle.Intel;
using NUnit.Framework;

namespace ChinaBettle.Tests.Intel;

/// <summary>
/// 招牌链路回归（情报规格 §4.1；真源 TRUST-09 桶预养 + DECP-02 痕迹存续 + SLICE-03②③）。
/// 预养把桶养过 0.8，再投放高可信假载荷 → AI 门控 Act（冒进）；
/// 冷桶直接投放同条假情报 → 只能 ForceRecheck。
/// </summary>
public sealed class ReduceStoveChainTests
{
    private const string Topic = "赵军_西线兵力";

    [Test]
    public void PrePrimedBucket_HighCredibilityFake_TriggersAct()
    {
        var cc = CredibilityConfig.Default;
        var tc = TrustConfig.Default;
        var trust = new TrustBucketSystem(tc);
        var decision = new global::ChinaBettle.AI.IntelDrivenDecision(trust, tc, cc);

        // 阶段一：预养（规格 §4.1 时间轴，更新量以该节数值为准）。
        // t=2:00 目视 65%，1 种来源、近期 → 桶 0.55
        trust.Update(Topic, 65f, TrustBucketSystem.IndependenceFactor(1, tc), 120f);
        // t=4:00 文书 80%，最近 3 条 2 种来源 → 桶 0.70
        trust.Update(Topic, 80f, TrustBucketSystem.IndependenceFactor(2, tc), 240f);
        // t=6:00 目视 64%，2 种来源 → 桶 0.77
        trust.Update(Topic, 64f, TrustBucketSystem.IndependenceFactor(2, tc), 360f);
        Assert.That(trust.Get(Topic), Is.EqualTo(0.77f).Within(0.02f));

        // 阶段二：t=11:00 假情报（减灶篡改后载荷 600，可信度 75，2 种来源、距上次>2 分钟 time=0.5）→ 0.83
        float primed = trust.Update(Topic, 75f, TrustBucketSystem.IndependenceFactor(2, tc), 660f);
        Assert.That(primed, Is.EqualTo(0.83f).Within(0.01f));

        var gate = DecisionGateEvaluator.Evaluate(CredibilityCalculator.Tier(75f, cc), primed, tc, cc);
        Assert.That(gate, Is.EqualTo(DecisionGate.Act), "桶>0.8 且情报 70–84 可信 → AI 应据假载荷冒进");
    }

    [Test]
    public void ColdBucket_SameFakeIntel_OnlyTriggersRecheck()
    {
        var cc = CredibilityConfig.Default;
        var trust = new TrustBucketSystem();
        // 先用 50% 情报做一次零增量更新（signed=0，桶仍 0.50），把"最近更新时刻"拨到 60s；
        // 否则全新桶首次更新按近期因子 1.0（情报规格 §4.1），吃不到陈旧因子 0.5。
        trust.Update(Topic, 50f, TrustBucketSystem.IndependenceFactor(1), 60f);
        // 冷桶 0.50 + 单种来源 + 距上次 10 分钟（陈旧 time=0.5）：0.50 + 0.5×0.6×0.5×0.3 = 0.545
        float cold = trust.Update(Topic, 75f, TrustBucketSystem.IndependenceFactor(1), 660f);
        Assert.That(cold, Is.EqualTo(0.545f).Within(0.005f));

        var gate = DecisionGateEvaluator.Evaluate(CredibilityCalculator.Tier(75f, cc), cold);
        Assert.That(gate, Is.EqualTo(DecisionGate.ForceRecheck));
    }

    [Test]
    public void DeceptionTrace_Lasts10Minutes_AndDoesNotRefresh()
    {
        var trace = new DeceptionTrace(StratagemDefinition.ReduceStove, castAtSeconds: 600f, "Area_A", Topic);

        Assert.Multiple(() =>
        {
            // 施计窗口内（90 秒）远观篡改
            Assert.That(trace.ApplyToFarObservation(2000f, observerDistanceMeters: 120f, nowSeconds: 650f), Is.EqualTo(600f));
            // 窗口结束后 10 分钟存续期内仍篡改
            Assert.That(trace.IsActiveAt(690f + 599f), Is.True);
            // 近距 50 米清点看到真值
            Assert.That(trace.ApplyToFarObservation(2000f, observerDistanceMeters: 50f, nowSeconds: 650f), Is.EqualTo(2000f));
            // 存续期满（90 + 600 = 690 秒后）失效
            Assert.That(trace.IsActiveAt(600f + 90f + 600f), Is.False);
            // DECP-08：存续期内重复释放被拒绝
            Assert.That(trace.AcceptRecast(700f), Is.False);
        });
    }

    [Test]
    public void FlaggedFake_ExitsAggregation_AndShows15Percent()
    {
        var pool = new IntelPool();
        var record = new IntelRecord
        {
            IntelId = "I1",
            Topic = Topic,
            SourceType = IntelSourceType.ScoutVisual,
            BaseCredibility = 65f,
            RawContent = new IntelPayload(IntelPayloadType.TroopCountEstimate, 600f),
            ScoutCount = 1,
            ObservationMinutes = 8f,
            CreatedAtSeconds = 0f,
        };
        record.Initialize(CredibilityCalculator.Fresh(CredibilityConfig.Default, IntelSourceType.ScoutVisual, 1, 8f));
        pool.Add(record);
        Assert.That(pool.AggregateCredibility(Topic, 0f), Is.EqualTo(75f));

        record.FlagAsFake(); // INTEL-08
        Assert.That(record.Tick(0f), Is.EqualTo(15f));
        Assert.That(pool.AggregateCredibility(Topic, 0f), Is.Null, "识破后退出综合计算，主题无其他活跃情报 → null");
    }

    [Test]
    public void CrossSourceDetection_RequiresHeterogeneousKindsAndDivergence()
    {
        // DECP-05：≥3 条含 ≥2 种来源类型、估算差异 ≥50%
        var pool = new IntelPool();
        IntelRecord Make(int i, IntelSourceType source, float value)
        {
            var r = new IntelRecord
            {
                IntelId = $"I{i}",
                Topic = Topic,
                SourceType = source,
                BaseCredibility = 65f,
                RawContent = new IntelPayload(IntelPayloadType.TroopCountEstimate, value),
                CreatedAtSeconds = i,
            };
            r.Initialize(70f);
            return r;
        }

        pool.Add(Make(1, IntelSourceType.ScoutVisual, 600f));
        pool.Add(Make(2, IntelSourceType.ScoutVisual, 600f));
        pool.Add(Make(3, IntelSourceType.ScoutVisual, 600f));
        Assert.That(DeceptionDetector.DetectByCrossSources(pool, Topic, 0f), Is.False,
            "同区域同质斥候假数据完全一致，3 条也不构成矛盾");

        pool.Add(Make(4, IntelSourceType.CapturedDocument, 2000f));
        Assert.That(DeceptionDetector.DetectByCrossSources(pool, Topic, 10f, window: 4), Is.True,
            "异质信源 + 233% 估算差异 → 识破");
    }
}
