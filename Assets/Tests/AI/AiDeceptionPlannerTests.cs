using ChinaBettle.AI;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Intel;
using ChinaBettle.Stratagems;
using NUnit.Framework;

namespace ChinaBettle.Tests.AI;

/// <summary>
/// AI 欺骗行为回归（真源 AI-05；SKILL-11 回收渠道）。
/// 锁定算例：劣势 0.7 倍率 → 增灶 ×1.5；可设伏 → 减灶 ×0.3；30 点/90s 与玩家同规则；
/// 玩家识破链路（远观篡改 → 近距真值 → 标伪 +15 点）全通。
/// </summary>
public sealed class AiDeceptionPlannerTests
{
    private const string Topic = "秦军_西线兵力";
    private const string AreaId = "Area_Qin_West";

    [Test]
    public void StrengthDisadvantage_TriggersAddStove_AndSpends30()
    {
        var wallet = new StrategyPointWallet();
        var planner = new AiDeceptionPlanner(wallet);

        // 600 < 0.7×1000=700 → 劣势 → 增灶示强。
        var cast = planner.TryCast(600f, 1000f, canAmbush: false, Topic, AreaId, trueTroopCount: 600f, nowSeconds: 120f);

        Assert.Multiple(() =>
        {
            Assert.That(cast, Is.Not.Null);
            Assert.That(cast!.Definition.Id, Is.EqualTo(ChinaBettle.Foundation.Stratagems.StratagemId.AddStove));
            Assert.That(cast.TrueTroopCount, Is.EqualTo(600f));
            Assert.That(cast.FabricatedTroopCount, Is.EqualTo(900f), "增灶 ×1.5");
            Assert.That(cast.SpentPoints, Is.EqualTo(30));
            Assert.That(wallet.Points, Is.EqualTo(70));
        });
    }

    [Test]
    public void AmbushWithoutDisadvantage_TriggersReduceStove()
    {
        var planner = new AiDeceptionPlanner(new StrategyPointWallet());

        // 1000 ≥ 0.7×1000 → 无劣势；可设伏 → 减灶示弱。
        var cast = planner.TryCast(1000f, 1000f, canAmbush: true, Topic, AreaId, 1000f, 60f);

        Assert.Multiple(() =>
        {
            Assert.That(cast, Is.Not.Null);
            Assert.That(cast!.Definition.Id, Is.EqualTo(ChinaBettle.Foundation.Stratagems.StratagemId.ReduceStove));
            Assert.That(cast.FabricatedTroopCount, Is.EqualTo(300f), "减灶 ×0.3");
        });
    }

    [Test]
    public void NoDisadvantageNoAmbush_DoesNotCast()
    {
        var planner = new AiDeceptionPlanner(new StrategyPointWallet());
        Assert.That(planner.TryCast(1000f, 1000f, canAmbush: false, Topic, AreaId, 1000f, 0f), Is.Null);
    }

    [Test]
    public void BattleCooldown_BlocksRecast_LikePlayer()
    {
        var planner = new AiDeceptionPlanner(new StrategyPointWallet());
        var first = planner.TryCast(600f, 1000f, false, Topic, AreaId, 600f, 120f);
        Assert.That(first, Is.Not.Null);

        // 90 秒战场 CD（SKILL-01/02）内重复释放被拒。
        Assert.That(planner.TryCast(600f, 1000f, false, Topic, AreaId, 600f, 170f), Is.Null, "CD 50 秒 < 90 秒");

        // CD 满后可再放（DECP-08 的同源痕迹拒重由 Trace 层另管）。
        var recast = planner.TryCast(600f, 1000f, false, Topic, AreaId, 600f, 120f + 90f);
        Assert.That(recast, Is.Not.Null);
    }

    [Test]
    public void InsufficientPoints_DoesNotCast()
    {
        var wallet = new StrategyPointWallet(initialPoints: 20);
        var planner = new AiDeceptionPlanner(wallet);
        Assert.That(planner.TryCast(600f, 1000f, false, Topic, AreaId, 600f, 0f), Is.Null);
        Assert.That(wallet.Points, Is.EqualTo(20), "施计失败不扣点");
    }

    [Test]
    public void PlayerDetectionChain_FarSeesFake_ProximitySeesTrue_DetectionAwards15()
    {
        var aiWallet = new StrategyPointWallet();
        var planner = new AiDeceptionPlanner(aiWallet);
        var cast = planner.TryCast(600f, 1000f, false, Topic, AreaId, trueTroopCount: 600f, nowSeconds: 120f);
        Assert.That(cast, Is.Not.Null);

        // 玩家远观新采样（120 米 > 50 米近距阈值）→ 看到篡改载荷。
        float farSeen = cast!.Trace.ApplyToFarObservation(600f, observerDistanceMeters: 120f, nowSeconds: 150f);
        Assert.That(farSeen, Is.EqualTo(900f), "远观吃到 ×1.5 篡改");

        // 玩家斥候近距 50 米清点 → 真值（识破路径①）。
        float closeSeen = cast.Trace.ApplyToFarObservation(600f, observerDistanceMeters: 50f, nowSeconds: 150f);
        Assert.That(closeSeen, Is.EqualTo(600f));
        Assert.That(DeceptionDetector.DetectByProximity(50f), Is.True);

        // 玩家侧钱包：识破奖励 +15（SKILL-11），AI 扣的 30 点与玩家钱包无关。
        var playerWallet = new StrategyPointWallet(initialPoints: 85);
        int gained = playerWallet.AwardDeceptionDetected();
        Assert.Multiple(() =>
        {
            Assert.That(gained, Is.EqualTo(15));
            Assert.That(playerWallet.Points, Is.EqualTo(100), "上限 100，溢出不累积");
            Assert.That(aiWallet.Points, Is.EqualTo(70));
        });
    }
}
