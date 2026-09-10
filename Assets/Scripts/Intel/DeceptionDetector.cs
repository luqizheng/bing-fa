using System;
using System.Linq;
using ChinaBettle.Foundation.Intel;

namespace ChinaBettle.Intel;

/// <summary>
/// 识破判定（情报规格 §4.3；真源 DECP-04/05）。两条硬路径任一满足即识破：
/// ①近距实体目击：斥候进入假情报生成点 50 米内清点营盘（远观炊烟升级）；
/// ②异质独立信源交叉：同主题 ≥3 条且含 ≥2 种不同来源类型、估算值最大差异 ≥50%。
/// 注意：同区域同质斥候看到的假数据完全一致，再多也不构成矛盾。
/// </summary>
public static class DeceptionDetector
{
    public static bool DetectByProximity(float observerDistanceMeters, DeceptionDetectionConfig? config = null)
    {
        var c = config ?? DeceptionDetectionConfig.Default;
        return observerDistanceMeters <= c.ProximityMeters;
    }

    public static bool DetectByCrossSources(
        IntelPool pool,
        string topic,
        float nowSeconds,
        int window = 3)
    {
        var c = DeceptionDetectionConfig.Default;
        pool.Tick(nowSeconds);

        var recent = pool.Records
            .Where(r => r.Topic == topic)
            .OrderByDescending(r => r.CreatedAtSeconds)
            .Take(window)
            .ToArray();

        if (recent.Length < c.RequiredIntelCount)
        {
            return false;
        }

        int kinds = recent.Select(r => r.SourceType).Distinct().Count();
        if (kinds < c.RequiredSourceKinds)
        {
            return false;
        }

        float min = recent.Min(r => r.RawContent.Value);
        float max = recent.Max(r => r.RawContent.Value);
        if (min <= 0f)
        {
            return false;
        }

        // 最大相对差异：以较小值为基准（600 vs 2000 → 233%）。
        float divergence = (max - min) / min;
        return divergence >= c.MinEstimateDivergence;
    }
}

/// <summary>识破参数（真源 DECP-04/05、INTEL-08、TRUST-06）。</summary>
public sealed record DeceptionDetectionConfig(
    float ProximityMeters = 50f,
    int RequiredIntelCount = 3,
    int RequiredSourceKinds = 2,
    float MinEstimateDivergence = 0.50f)
{
    public static DeceptionDetectionConfig Default { get; } = new();
}
