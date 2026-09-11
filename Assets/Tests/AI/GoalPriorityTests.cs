using System.Linq;
using ChinaBettle.Foundation.AI;
using NUnit.Framework;

namespace ChinaBettle.Tests.AI
{
    /// <summary>
    /// 目标优先级回归（真源 AI-02）：`priority = 成功收益×成功率 − 失败代价×(1−成功率) − 不行动代价`。
    ///
    /// 本文件锁定的是**公式结构与反死锁性质**（"不行动代价"必须能把目标顶到队首），
    /// 不锁定 P5 在白起 AI 里用的具体收益/代价取值（那些是内容参数，可调）。
    /// </summary>
    public sealed class GoalPriorityTests
    {
        [Test]
        public void Priority_MatchesFormula()
        {
            // 100×0.8 − 60×0.2 − 5 = 80 − 12 − 5 = 63
            var goal = new Goal("test", SuccessGain: 100f, SuccessRate: 0.8f, FailureCost: 60f, InactionCost: 5f);

            Assert.That(goal.Priority, Is.EqualTo(63f).Within(1e-3f));
        }

        [Test]
        public void InactionCost_IsSubtracted_NotIgnored()
        {
            var low = new Goal("hold", 40f, 0.7f, 10f, InactionCost: 0f);
            var high = new Goal("hold", 40f, 0.7f, 10f, InactionCost: 30f);

            Assert.That(high.Priority, Is.LessThan(low.Priority),
                "不行动代价必须真的扣减（AI-02：它是消除死锁的关键项）");
        }

        [Test]
        public void RisingInactionCost_PromotesAttackingGoal_EliminatingDeadlock()
        {
            // AI-02 的核心性质：断粮/围困越久，"继续干等"的代价越高，
            // 进攻/封锁目标最终必须超过固守目标——否则 AI 会站在壁垒前发呆（P1-2 死锁）。
            //
            // 语义前提：**固守的不行动代价被围困加重得最狠**（守着一座断粮的营垒＝坐以待毙，
            // 而夺粮道本身就是在解围）。故固守的 inactionCost 系数最高，这符合真源
            // "断粮等死类目标的 inactionCost 随时间上升"的表述。
            var prioritizer = new GoalPrioritizer();

            Goal[] GoalsAt(float siegePressure) => new[]
            {
                new Goal("夺点", SuccessGain: 100f, SuccessRate: 0.5f, FailureCost: 60f, InactionCost: siegePressure * 0.4f),
                new Goal("封锁粮道", SuccessGain: 70f, SuccessRate: 0.65f, FailureCost: 20f, InactionCost: siegePressure * 0.8f),
                new Goal("固守", SuccessGain: 40f, SuccessRate: 0.7f, FailureCost: 10f, InactionCost: siegePressure * 1.2f),
            };

            // 围困加深：固守必须被顶下去，否则死锁未消除。
            // 不断言"压力 0 时必须固守"——那取决于各目标的收益/代价取值（内容参数，可调）；
            // 本用例只锁定 AI-02 的**结构性质**：不行动代价随围困上升，必须能改变选择。
            var rankedAtZero = prioritizer.Rank(GoalsAt(0f));
            var rankedAtHundred = prioritizer.Rank(GoalsAt(100f));

            Assert.That(rankedAtZero.First().Key, Is.Not.EqualTo(rankedAtHundred.First().Key),
                "围困压力从 0 升到 100 后，首选目标应当改变——这正是 inactionCost 项存在的意义（AI-02）");

            // 且"固守"的排名必须因压力而下降（被推后或至少不再第一）。
            int holdRankAtZero = rankedAtZero.Select(g => g.Key).ToList().IndexOf("固守");
            int holdRankAtHundred = rankedAtHundred.Select(g => g.Key).ToList().IndexOf("固守");
            Assert.That(holdRankAtHundred, Is.GreaterThanOrEqualTo(holdRankAtZero),
                "围困越久，固守的排名不应上升（坐以待毙的代价在变大）");
        }

        [Test]
        public void LowConfidence_MakesHoldingRelativelyBetter()
        {
            var prioritizer = new GoalPrioritizer();

            // 情报不可信（成功率低）时，进攻的期望收益应显著下降 → 固守的相对价值上升。
            var goalsLowConfidence = new[]
            {
                new Goal("夺点", 100f, SuccessRate: 0.35f, FailureCost: 60f, InactionCost: 0f),
                new Goal("固守", 40f, SuccessRate: 0.7f, FailureCost: 10f, InactionCost: 0f),
            };

            var ranked = prioritizer.Rank(goalsLowConfidence);

            Assert.That(ranked.First().Key, Is.EqualTo("固守"),
                "情报不可信时不应冒进（成功率进公式的应有之义）");
        }

        [Test]
        public void Rank_IsDeterministic_AndDescending()
        {
            var goals = new[]
            {
                new Goal("a", 10f, 0.5f, 5f, 1f),
                new Goal("b", 30f, 0.5f, 5f, 1f),
                new Goal("c", 20f, 0.5f, 5f, 1f),
            };

            var ranked = new GoalPrioritizer().Rank(goals);

            Assert.That(ranked.Select(g => g.Key), Is.EqualTo(new[] { "b", "c", "a" }));
            Assert.That(ranked[0].Priority, Is.GreaterThanOrEqualTo(ranked[1].Priority));
            Assert.That(ranked[1].Priority, Is.GreaterThanOrEqualTo(ranked[2].Priority));

            // 无随机决策（GDD §5.2 v1.1 确定性要求）：同样输入必须给出同样排序。
            Assert.That(new GoalPrioritizer().Rank(goals).Select(g => g.Key), Is.EqualTo(ranked.Select(g => g.Key)));
        }

        [Test]
        public void Top_ReturnsNull_OnEmptyInput()
        {
            Assert.That(new GoalPrioritizer().Top(new Goal[0]), Is.Null);
        }
    }

}
