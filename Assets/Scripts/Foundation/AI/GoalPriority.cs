using System;
using System.Collections.Generic;
using System.Linq;

namespace ChinaBettle.Foundation.AI
{
    /// <summary>
    /// 目标优先级（真源 AI-02）：
    /// priority = 成功收益×成功率 − 失败代价×(1−成功率) − 不行动代价。
    /// "不行动代价"必须进公式：断粮时 inactionCost 随时间上升，把夺粮/撤退顶到队首，消除死锁。
    /// 纯逻辑、确定性，无随机决策（GDD §5.2 v1.1）。
    /// </summary>
    public sealed record Goal(
        string Key,
        float SuccessGain,
        float SuccessRate,
        float FailureCost,
        float InactionCost)
    {
        public float Priority =>
            SuccessGain * SuccessRate - FailureCost * (1f - SuccessRate) - InactionCost;
    }

    public sealed class GoalPrioritizer
    {
        public IReadOnlyList<Goal> Rank(IEnumerable<Goal> goals) =>
            goals.OrderByDescending(g => g.Priority).ToList();

        /// <summary>取优先级最高的目标（Unity 语言级别 C# 9 / .NET Standard 2.1，无 Enumerable.MaxBy）。</summary>
        public Goal? Top(IEnumerable<Goal> goals) =>
            goals.OrderByDescending(g => g.Priority).FirstOrDefault();
    }

}