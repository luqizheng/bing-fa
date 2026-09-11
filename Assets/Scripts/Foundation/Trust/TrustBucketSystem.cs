using System;
using System.Collections.Generic;

namespace ChinaBettle.Foundation.Trust
{
    /// <summary>
    /// AI 信念层：按情报主题分桶的信任度系统（情报规格 §5；真源 TRUST-01~06/09/11）。
    /// 桶语义（TRUST-09）：对主题【当前载荷】的总体采信度，0–1 标量，无方向——
    /// 不存在"信兵力多/信兵力少"两个桶；桶高 + 新载荷达可信档 = AI 采信当前载荷。
    /// 纯逻辑、无 Unity 依赖。
    /// </summary>
    public sealed class TrustBucketSystem
    {
        private readonly Dictionary<string, float> buckets = new();
        private readonly TrustConfig config;

        // 每桶最近一次更新的战役时间（秒），用于 TRUST-04 时间因子。
        private readonly Dictionary<string, float> lastUpdateSeconds = new();

        public TrustBucketSystem(TrustConfig? config = null)
        {
            this.config = config ?? TrustConfig.Default;
        }

        public float Get(string topic) => buckets.TryGetValue(topic, out var v) ? v : config.InitialValue;

        public IReadOnlyDictionary<string, float> Snapshot() => buckets;

        /// <summary>
        /// 增量更新（TRUST-02）：new = clamp(旧 + (c−0.5)×2 × ind × time × 0.3, 0.10, 0.95)。
        /// </summary>
        /// <param name="credibilityPercent">情报可信度 0–100。</param>
        /// <param name="independence">信源独立因子 0.6/0.8/1.0（TRUST-03）。</param>
        /// <param name="battleClockSeconds">当前战役时钟（秒）。</param>
        public float Update(string topic, float credibilityPercent, float independence, float battleClockSeconds)
        {
            float old = Get(topic);
            float timeFactor = TimeFactor(topic, battleClockSeconds);
            float signed = (credibilityPercent / 100f - 0.5f) * 2f;
            float next = Math.Clamp(old + signed * independence * timeFactor * config.UpdateStep, config.MinValue, config.MaxValue);
            buckets[topic] = next;
            lastUpdateSeconds[topic] = battleClockSeconds;
            return next;
        }

        /// <summary>识破处置（TRUST-06）：对应桶直接置 0.20，不走增量公式。</summary>
        public void SetDetected(string topic, float battleClockSeconds)
        {
            buckets[topic] = config.DetectedValue;
            lastUpdateSeconds[topic] = battleClockSeconds;
        }

        /// <summary>
        /// 局内反思负反馈（TRUST-11）：AI 因采信该主题情报而遭重大损失（≥30% 参战兵力/据点失守）时，
        /// 桶 ×0.5（默认），模拟"吃一堑长一智"。与 TRUST-06 识破区分：识破=情报层识破假情报，反思=行动层吃亏。
        /// 仅改数值，是否触发强制复核由上层 AI 决策负责。
        /// </summary>
        public float ApplyReflectionPenalty(string topic, float battleClockSeconds)
        {
            float next = Math.Clamp(Get(topic) * config.ReflectionPenaltyMultiplier, config.MinValue, config.MaxValue);
            buckets[topic] = next;
            lastUpdateSeconds[topic] = battleClockSeconds;
            return next;
        }

        /// <summary>信源独立因子（TRUST-03）：最近 3 条情报中不同来源类型数 → 1 种 0.6 / 2 种 0.8 / ≥3 种 1.0。</summary>
        public static float IndependenceFactor(int distinctSourceKinds, TrustConfig? config = null)
        {
            var c = config ?? TrustConfig.Default;
            return distinctSourceKinds >= 3 ? c.IndependenceThreeTypes
                 : distinctSourceKinds == 2 ? c.IndependenceTwoTypes
                 : c.IndependenceOneType;
        }

        /// <summary>
        /// 时间衰减因子（TRUST-04）：距该桶上次更新 ≤2 分钟 1.0，否则 0.5。
        /// 全新桶的首次更新按近期 1.0（与情报规格 §4.1 阶段一 t=2:00 首条 0.50→0.55 一致；
        /// 主题尚不存在时无所谓"陈旧"）。
        /// </summary>
        public float TimeFactor(string topic, float battleClockSeconds)
        {
            if (!lastUpdateSeconds.TryGetValue(topic, out var last))
            {
                return config.TimeFactorRecent;
            }

            float minutesSince = (battleClockSeconds - last) / 60f;
            return minutesSince <= config.RecentWindowMinutes ? config.TimeFactorRecent : config.TimeFactorStale;
        }
    }

}