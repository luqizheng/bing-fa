using System;

namespace ChinaBettle.Stratagems
{
    /// <summary>
    /// 谋略点经济（真源 SKILL-11）。
    /// 完整版：战略层每旬 +20、上限 100、跨旬不累积；战场内识破敌方一次诡计 +15（受上限约束）；
    /// 成功执行欺骗不产点（杜绝造假赚点）。原型：开局固定 100。
    /// 纯逻辑：战役层花费/识破奖励；每旬恢复由战略层流程调用 <see cref="GrantPerDecade"/>。
    /// </summary>
    public sealed class StrategyPointWallet
    {
        private readonly StrategyPointConfig config;

        public StrategyPointWallet(StrategyPointConfig? config = null, int? initialPoints = null)
        {
            this.config = config ?? StrategyPointConfig.Default;
            Points = initialPoints ?? this.config.PrototypeStartingPoints;
        }

        public int Points { get; private set; }

        public bool CanAfford(int cost) => Points >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            Points -= cost;
            return true;
        }

        /// <summary>识破敌方诡计奖励（SKILL-11）：+15，受 100 上限约束，溢出不累积。</summary>
        public int AwardDeceptionDetected()
        {
            int before = Points;
            Points = Math.Min(config.MaxPoints, Points + config.DetectReward);
            return Points - before;
        }

        /// <summary>战略层每旬恢复（SKILL-11）：+20、上限 100、跨旬不累积（即不补满，只加增量）。</summary>
        public int GrantPerDecade()
        {
            int before = Points;
            Points = Math.Min(config.MaxPoints, Points + config.PerDecadeRegen);
            return Points - before;
        }
    }

    /// <summary>谋略点参数（真源 SKILL-11）。</summary>
    public sealed record StrategyPointConfig(
        int MaxPoints = 100,
        int PerDecadeRegen = 20,
        int DetectReward = 15,
        int PrototypeStartingPoints = 100)
    {
        public static StrategyPointConfig Default { get; } = new();
    }

}