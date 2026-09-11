using System.Linq;
using ChinaBettle.Battle.Campaign;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 战后复盘回归（真源 GDD §5.4：编年史可回放、保留当时快照）。
    ///
    /// 锁定性质：按节拍采样（不会过密/过疏）、桶曲线与峰谷计算正确、
    /// 幕历史可回溯、摘要可读。不锁定具体采样条数之外的格式细节。
    /// </summary>
    public sealed class BattleReplayTests
    {
        private const string Topic = "赵军_兵力";

        [Test]
        public void Tick_SamplesOnlyOnInterval()
        {
            var replay = new BattleReplay();
            int recorded = 0;

            // 推进 30 秒（节拍 10 秒）→ 应恰好记录 3 次。
            for (int i = 0; i < 30; i++)
            {
                if (replay.Tick(1f, i + 1f, Topic, 0.5f, 70f, 1))
                {
                    recorded++;
                }
            }

            Assert.That(recorded, Is.EqualTo(3), "采样必须按固定节拍节流（10 秒一次）");
            Assert.That(replay.Samples, Has.Count.EqualTo(3));
        }

        [Test]
        public void BucketCurve_FollowsRecordedValues()
        {
            var replay = new BattleReplay();
            var values = new[] { 0.50f, 0.62f, 0.83f, 0.20f };

            for (int i = 0; i < values.Length; i++)
            {
                replay.Tick(BattleReplay.SampleIntervalSeconds, (i + 1) * BattleReplay.SampleIntervalSeconds,
                    Topic, values[i], 70f, i + 1);
            }

            var curve = replay.BucketCurve(Topic);

            Assert.That(curve.Select(p => p.Value), Is.EqualTo(values),
                "曲线应逐点对应（识破置 0.20 的低谷必须可见——TRUST-06 的可复盘性）");
            Assert.That(replay.PeakValue(Topic), Is.EqualTo(0.83f).Within(1e-4f));
            Assert.That(replay.TroughValue(Topic), Is.EqualTo(0.20f).Within(1e-4f));
        }

        [Test]
        public void BucketCurve_IsPerTopic()
        {
            var replay = new BattleReplay();
            replay.Tick(BattleReplay.SampleIntervalSeconds, 10f, "赵军_兵力", 0.5f, 70f, 1);
            replay.Tick(BattleReplay.SampleIntervalSeconds, 20f, "秦军_兵力", 0.9f, 80f, 2);

            Assert.That(replay.BucketCurve("赵军_兵力"), Has.Count.EqualTo(1));
            Assert.That(replay.BucketCurve("秦军_兵力").Single().Value, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(replay.BucketCurve("不存在的主题"), Is.Empty);
        }

        [Test]
        public void ActHistory_IsOrderedAndRetrievable()
        {
            var replay = new BattleReplay();
            replay.RecordAct(0f, "Standoff", "① 开局对峙");
            replay.RecordAct(60f, "Challenge", "② 挑战与拒战");
            replay.RecordAct(150f, "Dismissal", "③ 反间换将");

            Assert.That(replay.ActHistory.Select(a => a.Act),
                Is.EqualTo(new[] { "Standoff", "Challenge", "Dismissal" }));
            Assert.That(replay.ActHistory[2].AtSeconds, Is.EqualTo(150f));
        }

        [Test]
        public void BuildSummary_IncludesTrustTrajectoryAndActs()
        {
            var replay = new BattleReplay();
            replay.Tick(BattleReplay.SampleIntervalSeconds, 10f, Topic, 0.60f, 70f, 2);
            replay.Tick(BattleReplay.SampleIntervalSeconds, 20f, Topic, 0.83f, 75f, 4);
            replay.RecordAct(20f, "Dismissal", "③ 反间换将");

            string summary = replay.BuildSummary();

            Assert.That(summary, Does.Contain("0.60"), "摘要应含桶值起点");
            Assert.That(summary, Does.Contain("0.83"), "摘要应含桶值终点");
            Assert.That(summary, Does.Contain("反间换将"), "摘要应含幕推进");
        }

        [Test]
        public void BuildSummary_OnEmptyReplay_DoesNotThrow()
        {
            var replay = new BattleReplay();

            Assert.That(() => replay.BuildSummary(), Throws.Nothing);
            Assert.That(replay.BuildSummary(), Does.Contain("0 条"));
        }

        [Test]
        public void PeakAndTrough_OnEmptyTopic_AreZero_NotCrash()
        {
            var replay = new BattleReplay();

            Assert.That(replay.PeakValue("无"), Is.Zero);
            Assert.That(replay.TroughValue("无"), Is.Zero);
        }
    }

}
