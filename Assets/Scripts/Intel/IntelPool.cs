using System;
using System.Collections.Generic;
using System.Linq;
using ChinaBettle.Foundation.Intel;

namespace ChinaBettle.Intel;

/// <summary>
/// 单个接收方的情报池：负责年龄结算、同主题综合可信度（INTEL-05）、分档（INTEL-06）。
/// 玩家与 AI 各自持有独立实例，共用同一套规则引擎（情报规格 §1 核心约束）。
/// </summary>
public sealed class IntelPool
{
    private readonly List<IntelRecord> records = new();
    private readonly CredibilityConfig config;

    public IntelPool(CredibilityConfig? config = null)
    {
        this.config = config ?? CredibilityConfig.Default;
    }

    public IReadOnlyList<IntelRecord> Records => records;

    public void Add(IntelRecord record)
    {
        records.Add(record);
    }

    /// <summary>推进所有情报年龄并返回过期事件（调用方可用于编年史快照）。</summary>
    public void Tick(float nowSeconds)
    {
        foreach (var r in records)
        {
            r.Tick(nowSeconds, config);
        }
    }

    /// <summary>
    /// 同主题综合可信度（§3.2）：仅汇总 Active 条目（过期/识破剔除）。
    /// 无活跃情报返回 null（调用方按"无情报"处理，区别于可信度 0）。
    /// </summary>
    public float? AggregateCredibility(string topic, float nowSeconds)
    {
        Tick(nowSeconds);
        var active = records
            .Where(r => r.Topic == topic && r.Status == IntelStatus.Active)
            .Select(r => (Credibility: r.Tick(nowSeconds, config), r.SourceType))
            .ToArray();
        return CredibilityCalculator.Aggregate(active, config);
    }

    public CredibilityTier? Tier(string topic, float nowSeconds)
    {
        float? agg = AggregateCredibility(topic, nowSeconds);
        return agg.HasValue ? CredibilityCalculator.Tier(agg.Value, config) : null;
    }

    /// <summary>该主题最近 N 条（含本次）的不同来源类型数（TRUST-03）；同一欺骗技能产出识破前算 1 种。</summary>
    public int DistinctSourceKinds(string topic, int window = 3)
    {
        return records
            .Where(r => r.Topic == topic)
            .OrderByDescending(r => r.CreatedAtSeconds)
            .Take(window)
            .Select(r => r.SourceType)
            .Distinct()
            .Count();
    }
}
