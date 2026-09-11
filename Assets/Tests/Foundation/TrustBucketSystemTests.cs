using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Trust;
using NUnit.Framework;

namespace ChinaBettle.Tests.Foundation
{
    /// <summary>信任桶双向更新与门控回归（真源 TRUST-02/06；情报规格 §5.2 算例）。</summary>
    public sealed class TrustBucketSystemTests
    {
        private const string Topic = "赵军_西线兵力";

        [Test]
        public void Update_SpecExample_68Percent_TwoKinds_Recent_To059()
        {
            // 情报规格 §5.2 算例：0.50 + 0.36×0.8×1.0×0.3 ≈ 0.586（距上次更新 1 分钟）。
            // 先做一次历史更新建立"该主题已存在且近期更新"的前置，再以 60 秒后更新复现 time=1.0。
            var systemUnderTest = new TrustBucketSystem();
            systemUnderTest.Update(Topic, credibilityPercent: 50f, independence: 0.6f, battleClockSeconds: 0f);
            float next = systemUnderTest.Update(Topic, credibilityPercent: 68f, independence: 0.8f, battleClockSeconds: 60f);
            Assert.That(next, Is.EqualTo(0.586f).Within(0.005f));
        }

        [Test]
        public void Update_LowCredibility_LowersTrust()
        {
            var systemUnderTest = new TrustBucketSystem();
            float high = systemUnderTest.Update(Topic, 90f, 1.0f, 0f);
            float low = systemUnderTest.Update(Topic, 20f, 1.0f, 30f);
            Assert.That(low, Is.LessThan(high), "有符号更新：低可信情报必须拉低信任桶");
        }

        [Test]
        public void Detect_SetsBucketTo0_2()
        {
            var systemUnderTest = new TrustBucketSystem();
            systemUnderTest.Update(Topic, 90f, 1.0f, 0f);
            systemUnderTest.SetDetected(Topic, 60f);
            Assert.That(systemUnderTest.Get(Topic), Is.EqualTo(0.20f));
        }

        [Test]
        public void ReflectionPenalty_HalvesBucket()
        {
            // TRUST-11：采信吃亏后桶 ×0.5，与识破置 0.2 区分。
            var systemUnderTest = new TrustBucketSystem();
            systemUnderTest.Update(Topic, 90f, 1.0f, 0f); // 推高桶
            float before = systemUnderTest.Get(Topic);
            float after = systemUnderTest.ApplyReflectionPenalty(Topic, 60f);
            Assert.That(after, Is.EqualTo(before * 0.5f).Within(0.005f));
        }

        [Test]
        public void IndependenceFactor_FollowsDistinctSourceKinds()
        {
            Assert.That(TrustBucketSystem.IndependenceFactor(1), Is.EqualTo(0.6f));
            Assert.That(TrustBucketSystem.IndependenceFactor(2), Is.EqualTo(0.8f));
            Assert.That(TrustBucketSystem.IndependenceFactor(4), Is.EqualTo(1.0f));
        }

        [Test]
        public void Gate_DoubtfulIntel_AlwaysRecheck_RegardlessOfBucket()
        {
            // TRUST-05/07：50–69% 存疑档无论桶多高都强制复核（防温水煮青蛙）。
            var gate = DecisionGateEvaluator.Evaluate(CredibilityTier.Doubtful, trustBucketValue: 0.95f);
            Assert.That(gate, Is.EqualTo(DecisionGate.ForceRecheck));
        }

        [Test]
        public void Gate_LowBucket_OverridesTrustedIntel()
        {
            var gate = DecisionGateEvaluator.Evaluate(CredibilityTier.Trusted, trustBucketValue: 0.29f);
            Assert.That(gate, Is.EqualTo(DecisionGate.ForceRecheck));
        }

        [Test]
        public void Gate_NarrativeEvent_RequiresDoubleHigh()
        {
            // TRUST-08：综合 ≥85% 且桶 >0.8 才是 Confident（叙事双高）。
            Assert.That(
                DecisionGateEvaluator.Evaluate(CredibilityTier.Trusted, 0.95f),
                Is.EqualTo(DecisionGate.Act));
            Assert.That(
                DecisionGateEvaluator.Evaluate(CredibilityTier.Confident, 0.95f),
                Is.EqualTo(DecisionGate.Confident));
            Assert.That(
                DecisionGateEvaluator.Evaluate(CredibilityTier.Confident, 0.80f),
                Is.EqualTo(DecisionGate.ForceRecheck));
        }
    }

}