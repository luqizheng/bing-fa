using ChinaBettle.Battle.Campaign;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 教程引导回归（计划 §3 P6-2 的五步认知顺序）。
    ///
    /// 锁定性质：步骤顺序固定、每步条件独立成立、可连锁推进、完成后不再推进、
    /// 以及"非阻塞"（教程不因玩家跳步而失效）。
    /// </summary>
    public sealed class TutorialGuideTests
    {
        private static TutorialContext Ctx(
            int scoutIntel = 0,
            bool sawTampered = false,
            bool proximity = false,
            int casts = 0,
            int detections = 0,
            bool finished = false) =>
            new TutorialContext
            {
                ActiveScoutIntelCount = scoutIntel,
                SawTamperedPayload = sawTampered,
                DidProximityCheck = proximity,
                PlayerCastCount = casts,
                PlayerDetectionCount = detections,
                BattleFinished = finished,
            };

        [Test]
        public void StartsAtScouting()
        {
            var guide = new TutorialGuide();

            Assert.That(guide.Current, Is.EqualTo(TutorialStep.Scout));
            Assert.That(guide.CurrentHint, Does.Contain("斥候"));
            Assert.That(guide.IsCompleted, Is.False);
        }

        [Test]
        public void AdvancesInCanonicalOrder()
        {
            var guide = new TutorialGuide();

            guide.Step(Ctx(scoutIntel: 1));
            Assert.That(guide.Current, Is.EqualTo(TutorialStep.DiscernStove));

            guide.Step(Ctx(scoutIntel: 1, sawTampered: true));
            Assert.That(guide.Current, Is.EqualTo(TutorialStep.CastDeception));

            guide.Step(Ctx(scoutIntel: 1, sawTampered: true, casts: 1));
            Assert.That(guide.Current, Is.EqualTo(TutorialStep.DetectDeception));

            guide.Step(Ctx(scoutIntel: 1, sawTampered: true, casts: 1, detections: 1));
            Assert.That(guide.Current, Is.EqualTo(TutorialStep.Review));

            guide.Step(Ctx(finished: true));
            Assert.That(guide.IsCompleted, Is.True);
            Assert.That(guide.Current, Is.EqualTo(TutorialStep.Completed));
        }

        [Test]
        public void DoesNotSkipAhead_WhenOnlyLaterConditionsHold()
        {
            var guide = new TutorialGuide();

            // 只满足"施计"却不满足"侦查" → 教程不得跳过①。
            guide.Step(Ctx(casts: 5, detections: 5, finished: true));

            Assert.That(guide.Current, Is.EqualTo(TutorialStep.Scout),
                "教程必须按认知顺序推进（先看见情报，再理解情报可假）");
        }

        [Test]
        public void ChainedAdvance_WhenSeveralConditionsAlreadyHold()
        {
            var guide = new TutorialGuide();

            // 一帧内①②同时成立 → 应连锁推到③，不要求玩家多走一趟。
            bool advanced = guide.Step(Ctx(scoutIntel: 1, sawTampered: true));

            Assert.That(advanced, Is.True);
            Assert.That(guide.Current, Is.EqualTo(TutorialStep.CastDeception));
        }

        [Test]
        public void ProximityCheck_AlsoSatisfiesDiscernStep()
        {
            var guide = new TutorialGuide();
            guide.Step(Ctx(scoutIntel: 1));

            // 玩家没有见到假载荷，但主动近距清点过 → ②同样算完成（认知目标已达成）。
            guide.Step(Ctx(scoutIntel: 1, proximity: true));

            Assert.That(guide.Current, Is.EqualTo(TutorialStep.CastDeception));
        }

        [Test]
        public void CompletedGuide_StopsAdvancing()
        {
            var guide = new TutorialGuide();
            guide.Step(Ctx(scoutIntel: 1, sawTampered: true, casts: 1, detections: 1, finished: true));

            Assert.That(guide.IsCompleted, Is.True);
            Assert.That(guide.Step(Ctx(scoutIntel: 99, sawTampered: true, casts: 99, detections: 99, finished: true)),
                Is.False, "完成后不再推进");
            Assert.That(guide.CurrentHint, Does.Contain("自由推演"), "完成后应转为自由提示（教程非阻塞）");
        }

        [Test]
        public void AdvanceMessage_IsExposedForHud()
        {
            var guide = new TutorialGuide();

            guide.Step(Ctx(scoutIntel: 1));

            Assert.That(guide.LastAdvancedMessage, Is.Not.Null);
            Assert.That(guide.LastAdvancedMessage, Does.Contain("侦查"));
        }
    }

}
