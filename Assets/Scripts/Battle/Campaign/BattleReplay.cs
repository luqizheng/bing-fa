using System.Collections.Generic;

namespace ChinaBettle.Battle.Campaign
{
    /// <summary>复盘里的一条时间轴采样（真源 GDD §5.4：战后可回放、保留当时快照）。</summary>
    public sealed record ReplaySample(
        float AtSeconds,
        string Topic,
        float BucketValue,
        float? AggregateCredibility,
        int ActiveIntelCount);

    /// <summary>
    /// 战后复盘数据（GDD §5.4）。
    ///
    /// 设计取舍：复盘是**只读快照的累积**，不重跑仿真——因为（真源情报规格 §5.2）
    /// 决策是确定性的，重跑没有意义，而快照能保证"当时看到的就是当时算的"。
    /// 纯逻辑、无 Unity 依赖，故可单测；表现层只负责画。
    /// </summary>
    public sealed class BattleReplay
    {
        private readonly List<ReplaySample> samples = new();
        private readonly List<(float AtSeconds, string Act, string Title)> actHistory = new();

        /// <summary>采样节拍（战场秒）：与情报观察节拍同量级，避免曲线过密。</summary>
        public const float SampleIntervalSeconds = 10f;

        private float accumulator;

        public IReadOnlyList<ReplaySample> Samples => samples;

        /// <summary>幕推进轨迹（CP-02）：进入时刻 + 幕名，用于复盘"哪一幕发生了什么"。</summary>
        public IReadOnlyList<(float AtSeconds, string Act, string Title)> ActHistory => actHistory;

        /// <summary>
        /// 按固定节拍采样一次。返回本次是否真的记录了（调用方不必关心节流）。
        /// </summary>
        public bool Tick(
            float deltaSeconds,
            float nowSeconds,
            string topic,
            float bucketValue,
            float? aggregateCredibility,
            int activeIntelCount)
        {
            accumulator += deltaSeconds;
            if (accumulator < SampleIntervalSeconds)
            {
                return false;
            }

            accumulator -= SampleIntervalSeconds;
            samples.Add(new ReplaySample(nowSeconds, topic, bucketValue, aggregateCredibility, activeIntelCount));
            return true;
        }

        /// <summary>记录一次幕推进。</summary>
        public void RecordAct(float nowSeconds, string act, string title) =>
            actHistory.Add((nowSeconds, act, title));

        /// <summary>取某主题的桶值轨迹（用于画曲线）。</summary>
        public List<(float AtSeconds, float Value)> BucketCurve(string topic)
        {
            var curve = new List<(float, float)>();
            foreach (var s in samples)
            {
                if (s.Topic == topic)
                {
                    curve.Add((s.AtSeconds, s.BucketValue));
                }
            }

            return curve;
        }

        /// <summary>
        /// 生成复盘摘要文本（结算面板与复盘视图共用）。
        /// 注意：**桶值可以给玩家看**——TRUST-10 禁止的是"实时决策期暴露桶值"，
        /// 战后复盘属 GDD §5.4 明确允许的"敌方视角赛后解锁"。
        /// </summary>
        public string BuildSummary()
        {
            var lines = new List<string>();

            lines.Add($"采样点 {samples.Count} 条 / 幕推进 {actHistory.Count} 次");

            if (samples.Count > 0)
            {
                var first = samples[0];
                var last = samples[samples.Count - 1];
                lines.Add($"主题「{last.Topic}」信任桶：{first.BucketValue:0.00} → {last.BucketValue:0.00}" +
                          $"（峰值 {PeakValue(last.Topic):0.00}，谷值 {TroughValue(last.Topic):0.00}）");
                lines.Add($"情报条数：{first.ActiveIntelCount} → {last.ActiveIntelCount}");
            }

            foreach (var (at, act, title) in actHistory)
            {
                lines.Add($"[{(int)(at / 60f):00}:{(int)(at % 60f):00}] {act}｜{title}");
            }

            return string.Join("\n", lines);
        }

        public float PeakValue(string topic)
        {
            float peak = float.MinValue;
            foreach (var s in samples)
            {
                if (s.Topic == topic && s.BucketValue > peak)
                {
                    peak = s.BucketValue;
                }
            }

            return peak == float.MinValue ? 0f : peak;
        }

        public float TroughValue(string topic)
        {
            float trough = float.MaxValue;
            foreach (var s in samples)
            {
                if (s.Topic == topic && s.BucketValue < trough)
                {
                    trough = s.BucketValue;
                }
            }

            return trough == float.MaxValue ? 0f : trough;
        }
    }
}
