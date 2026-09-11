using System.Collections.Generic;
using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 长平剧本回归（真源 CP-01…08、ADV-07、SKILL-13）。
    ///
    /// 本文件只测**剧本层**（纯逻辑）：幕序推进、换将事件、合围触发、双向结局判定；
    /// 时间阈值本身标【拟设】（待评审回写真源），故测试用**构造上下文**驱动，
    /// 不依赖真实时长的巧合——改阈值不应改这些用例。
    /// </summary>
    public sealed class ChangpingCampaignTests
    {
        private static CampaignContext Ctx(
            float elapsed = 0f,
            int zhaoUnits = 9,
            int qinUnits = 8,
            int zhaoInitial = 9,
            int beyondWall = 0,
            bool qinHoldsFord = false,
            bool corridorCut = false,
            bool zhaoGranaryLost = false,
            bool zhaoCampLost = false,
            float aiTrust = 0.5f,
            float playerTrust = 0.5f,
            bool brokeOut = false) =>
            new CampaignContext
            {
                ElapsedSeconds = elapsed,
                TimeLimitSeconds = 20f * 60f,
                ZhaoUnits = zhaoUnits,
                QinUnits = qinUnits,
                ZhaoInitialUnits = zhaoInitial,
                ZhaoUnitsBeyondWall = beyondWall,
                QinHoldsFord = qinHoldsFord,
                QinCutSupplyCorridor = corridorCut,
                ZhaoGranaryLost = zhaoGranaryLost,
                ZhaoCampLost = zhaoCampLost,
                AiTrustOnZhao = aiTrust,
                PlayerTrustOnQin = playerTrust,
                ZhaoBrokeOut = brokeOut,
            };

        // ───────────────────────── 幕序（CP-02）─────────────────────────

        [Test]
        public void Acts_CoverSevenActsInCanonicalOrder()
        {
            var acts = ChangpingCampaign.Acts.Select(a => a.Act).ToArray();

            Assert.That(acts, Is.EqualTo(new[]
            {
                CampaignAct.Standoff,
                CampaignAct.Challenge,
                CampaignAct.Dismissal,
                CampaignAct.Sortie,
                CampaignAct.Encirclement,
                CampaignAct.Siege,
                CampaignAct.Breakout,
            }), "CP-02：七幕顺序固定");
        }

        [Test]
        public void FirstStep_EntersStandoff()
        {
            var runner = new CampaignRunner();
            var result = runner.Step(Ctx());

            Assert.That(result.Act, Is.EqualTo(CampaignAct.Standoff));
            Assert.That(runner.CurrentTitle, Does.Contain("对峙"));
            Assert.That(result.Events.OfType<CampaignActEntered>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void DismissalAct_InjectsNarrativePayload_NotAFabrication()
        {
            var runner = new CampaignRunner();
            runner.Step(Ctx());                       // → ①
            runner.AdvanceOneAct(Ctx());              // → ②
            var events = runner.AdvanceOneAct(Ctx()); // → ③

            Assert.That(runner.Current, Is.EqualTo(CampaignAct.Dismissal));

            var inject = events.OfType<NarrativeIntelInject>().Single();
            Assert.That(inject.Recipient, Is.EqualTo(Faction.Zhao), "换将载荷发给赵方（玩家）");
            Assert.That(inject.Topic, Is.EqualTo("秦军_兵力"), "主题＝赵方关心的秦军兵力");
            Assert.That(inject.SourceKind, Is.Not.EqualTo(IntelSourceType.SkillReveal),
                "SKILL-13：换将是叙事事件，不是技能暴露");
            Assert.That(inject.Text, Does.Contain("反间"), "文案点明反间换将（ADV-07）");
        }

        [Test]
        public void SortieRatio_ThresholdDrivesEncirclement()
        {
            // 出垒 5/9 ≈ 0.56 < 0.60 → 仍在出垒幕。
            Assert.That(ChangpingCampaign.SortieRatio(Ctx(beyondWall: 5, zhaoInitial: 9)),
                Is.LessThan(ChangpingCampaign.SortieRatioThreshold));

            // 出垒 6/9 ≈ 0.67 ≥ 0.60 → 满足推进条件（CP-06③）。
            Assert.That(ChangpingCampaign.SortieRatio(Ctx(beyondWall: 6, zhaoInitial: 9)),
                Is.GreaterThanOrEqualTo(ChangpingCampaign.SortieRatioThreshold));

            // 初始兵力为 0 时不得除零。
            Assert.That(ChangpingCampaign.SortieRatio(Ctx(zhaoInitial: 0)), Is.Zero);
        }

        [Test]
        public void CorridorCut_AdvancesFromEncirclement()
        {
            var runner = new CampaignRunner();
            runner.Step(Ctx());
            while (runner.Current != CampaignAct.Encirclement)
            {
                runner.AdvanceOneAct(Ctx(elapsed: 200f, beyondWall: 9));
            }

            var result = runner.Step(Ctx(elapsed: 200f, corridorCut: true));
            Assert.That(result.Act, Is.EqualTo(CampaignAct.Siege), "CP-06①：粮道被切断 → 进入断粮围困");
            Assert.That(result.Events.OfType<HoldOrder>().Any(), Is.True, "秦军转入封锁态势");
        }

        // ───────────────────────── 结局分支（CP-08）─────────────────────────

        [Test]
        public void HistoricalEnding_WhenZhaoMainForceDestroyed()
        {
            var ending = ChangpingCampaign.EvaluateEnding(
                Ctx(zhaoUnits: 1, zhaoInitial: 9), CampaignAct.Encirclement);

            Assert.That(ending, Is.Not.Null);
            Assert.That(ending!.PlayerWins, Is.False);
            Assert.That(ending.Id, Does.Contain("史实结局"));
        }

        [Test]
        public void HistoricalEnding_WhenZhaoAnnihilatedInSiege()
        {
            var ending = ChangpingCampaign.EvaluateEnding(Ctx(zhaoUnits: 0), CampaignAct.Siege);

            Assert.That(ending, Is.Not.Null);
            Assert.That(ending!.PlayerWins, Is.False);
        }

        [Test]
        public void RevisedEnding_WhenBreakingOutDuringBreakoutAct()
        {
            var ending = ChangpingCampaign.EvaluateEnding(
                Ctx(zhaoUnits: 6, zhaoInitial: 9, brokeOut: true), CampaignAct.Breakout);

            Assert.That(ending, Is.Not.Null);
            Assert.That(ending!.PlayerWins, Is.True);
            Assert.That(ending.Id, Does.Contain("改写结局"));
        }

        [Test]
        public void RevisedEnding_WhenHoldingWestLeiUntilTimeLimit()
        {
            // 未进入围困就到达硬上限 → 守住西垒的改写分支（CP-08②）。
            var ending = ChangpingCampaign.EvaluateEnding(
                Ctx(elapsed: 20f * 60f), CampaignAct.Challenge);

            Assert.That(ending, Is.Not.Null);
            Assert.That(ending!.PlayerWins, Is.True);
            Assert.That(ending.Id, Does.Contain("守住西垒"));
        }

        [Test]
        public void NoEnding_WhileBattleOngoing()
        {
            Assert.That(ChangpingCampaign.EvaluateEnding(Ctx(), CampaignAct.Standoff), Is.Null);
            Assert.That(ChangpingCampaign.EvaluateEnding(Ctx(beyondWall: 3), CampaignAct.Sortie), Is.Null);
        }

        // ───────────────────────── 运行器集成 ─────────────────────────

        [Test]
        public void Runner_RunsToEnding_WithoutExceptions()
        {
            var runner = new CampaignRunner();
            var ctx = Ctx();

            // 第 ① 步进入第 ① 幕。
            runner.Step(ctx);

            // 推进到出垒并大量减员 → 应结算为史实结局。
            runner.AdvanceOneAct(ctx);                       // ②
            runner.AdvanceOneAct(ctx);                       // ③
            runner.AdvanceOneAct(ctx);                       // ④
            runner.AdvanceOneAct(ctx);                       // ⑤

            var result = runner.Step(Ctx(zhaoUnits: 1, zhaoInitial: 9, elapsed: 300f));

            Assert.That(runner.Ending, Is.Not.Null, "损失逾 80% 应结算（CP-08①）");
            Assert.That(runner.Current, Is.EqualTo(CampaignAct.Finished));
            Assert.That(result.Events.OfType<CampaignEnd>().Any(), Is.True);
            Assert.That(runner.History.Count, Is.GreaterThanOrEqualTo(5), "幕历史可用于复盘");
        }

        [Test]
        public void Runner_EndingIsTerminal()
        {
            var runner = new CampaignRunner();
            runner.Step(Ctx());
            runner.Step(Ctx(zhaoUnits: 0));   // 立即结算
            Assert.That(runner.Ending, Is.Not.Null);

            var after = runner.Step(Ctx(zhaoUnits: 0, elapsed: 999f));
            Assert.That(after.Advanced, Is.False, "结算后不再推进");
            Assert.That(after.Events, Is.Empty);
        }
    }

}
