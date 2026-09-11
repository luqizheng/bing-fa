using System;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Foundation.Combat
{
    /// <summary>
    /// 火攻火区结算器（真源 SKILL-03）：起火后持续 60 战场秒，区内单位持续灼烧 8 点/2 秒。
    /// 起火即向所有有视野一方生成 skill_reveal 真实情报——由 Intel 层 <c>FireRevealFactory</c> 承接，
    /// 本类只负责灼烧数值结算，不产情报、不读 Unity 时钟（战役时间由调用方传入，TIME-02 时钟纪律）。
    /// 纯逻辑、无 Unity 依赖。
    /// </summary>
    public sealed class FireZone
    {
        private readonly CombatConfig config;

        /// <summary>结算水位：已结算到的战役时刻（秒），支持不规则帧间隔的增量结算。</summary>
        private float settledThroughSeconds;

        public FireZone(float createdAtSeconds, CombatConfig? config = null)
        {
            this.config = config ?? CombatConfig.Default;
            CreatedAtSeconds = createdAtSeconds;
            settledThroughSeconds = createdAtSeconds;
        }

        /// <summary>起火时刻（战役时钟，秒）。</summary>
        public float CreatedAtSeconds { get; }

        /// <summary>火区中心（米，地图坐标）；纯逻辑层不引入坐标类型依赖，表现层自行组装。</summary>
        public float CenterX { get; init; }

        public float CenterZ { get; init; }

        /// <summary>火区作用半径（米）。</summary>
        public float Radius { get; init; } = 26f;

        /// <summary>火区熄灭时刻（战场秒）：起火 + 60（SKILL-03）。</summary>
        public float EndSeconds => CreatedAtSeconds + config.FireZoneDurationSeconds;

        /// <summary>火区是否仍在燃烧（含边界语义：起火时刻算燃烧，熄灭时刻不算）。</summary>
        public bool IsActiveAt(float nowSeconds) => nowSeconds >= CreatedAtSeconds && nowSeconds < EndSeconds;

        /// <summary>
        /// 结算 (已结算水位, now] 区间内的灼烧伤害并推进水位，返回本段伤害值。
        /// 灼烧 tick 发生在起火后每 2 战场秒整（起火+2、+4、…）；火区熄灭后不再产生伤害，
        /// 越过熄灭时刻的调用只结算到熄灭为止（不补炸、不追溯）。
        /// </summary>
        public int Settle(float nowSeconds)
        {
            int ticksBefore = TotalTicks(settledThroughSeconds);
            int ticksNow = TotalTicks(nowSeconds);
            settledThroughSeconds = Math.Max(settledThroughSeconds, nowSeconds);
            return Math.Max(0, ticksNow - ticksBefore) * config.FireZoneBurnDamagePerTick;
        }

        /// <summary>截至某时刻累计发生的灼烧 tick 数（按熄灭时刻截断）。</summary>
        private int TotalTicks(float nowSeconds)
        {
            float effective = Math.Min(Math.Max(nowSeconds, CreatedAtSeconds), EndSeconds);
            float elapsed = effective - CreatedAtSeconds;
            return (int)MathF.Floor(elapsed / config.FireZoneBurnIntervalSeconds);
        }
    }

}