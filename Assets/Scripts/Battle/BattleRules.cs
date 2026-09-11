using ChinaBettle.Foundation.AI;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Supply;
using ChinaBettle.Foundation.Trust;
using ChinaBettle.Foundation.Units;
using ChinaBettle.Intel;
using ChinaBettle.Stratagems;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 切片战役规则装配：把各真源 Config 与**切片内容参数**集中注入仿真，逻辑层不写魔法数字。
    /// 真源参数行均有 ID 注释；内容参数（地图/节奏）标注为切片占位，待定案后回写真源。
    /// </summary>
    public sealed class BattleRules
    {
        public CombatConfig Combat { get; init; } = CombatConfig.Default;

        public SupplyConfig Supply { get; init; } = SupplyConfig.Default;

        public CredibilityConfig Credibility { get; init; } = CredibilityConfig.Default;

        public TrustConfig Trust { get; init; } = TrustConfig.Default;

        public StrategyPointConfig Points { get; init; } = StrategyPointConfig.Default;

        public AiDeceptionConfig AiDeception { get; init; } = AiDeceptionConfig.Default;

        public DeceptionDetectionConfig Detection { get; init; } = DeceptionDetectionConfig.Default;

        /// <summary>TIME-04：单局 20 战场分钟硬上限（到时按胜负条件结算）。</summary>
        public float TimeLimitSeconds { get; init; } = 20f * 60f;

        /// <summary>情报规格："连续目击"每 10 战场秒采样一次（切片占位节奏）。</summary>
        public float ObservationIntervalSeconds { get; init; } = 10f;

        /// <summary>斥候视野半径（切片占位，米）。</summary>
        public float ScoutSightRadius { get; init; } = 90f;

        /// <summary>占领据点所需连续驻留（切片占位，秒）。</summary>
        public float CaptureHoldSeconds { get; init; } = 10f;

        /// <summary>AI 决策节拍（切片占位，秒）。</summary>
        public float AiThinkIntervalSeconds { get; init; } = 5f;

        /// <summary>TRUST-07 存疑型强复核的冻结时长下界（真源 30–60 战场秒）。</summary>
        public float StrongRecheckSeconds { get; init; } = 30f;

        /// <summary>粮仓补给半径（切片占位，米）：己方单位在此范围内自动补满携粮。</summary>
        public float GranaryResupplyRadius { get; init; } = 30f;

        /// <summary>FOOD-04：饥饿度到 100% 时单位饿散。</summary>
        public float StarvationDisbandThreshold { get; init; } = 1.0f;
    }

}