using System;
using ChinaBettle.Foundation.Intel;

namespace ChinaBettle.Intel
{
    /// <summary>
    /// 情报载荷类型（情报规格 §8）。切片使用 TroopCountEstimate 与 FireReveal。
    /// </summary>
    public enum IntelPayloadType
    {
        TroopCountEstimate,
        FireReveal,
    }

    /// <summary>
    /// 接收方可见的情报内容块（情报规格 §8.1 raw_content）。
    /// 注意：Unity 6 的脚本语言级别是 C# 9，不能用 record struct（C# 10），故手写等价结构
    /// （位置构造 + 只读属性 + 结构相等 + Deconstruct，语义与原 record struct 一致）。
    /// </summary>
    public readonly struct IntelPayload : IEquatable<IntelPayload>
    {
        public IntelPayload(IntelPayloadType type, float value, string unit = "men")
        {
            Type = type;
            Value = value;
            Unit = unit;
        }

        public IntelPayloadType Type { get; }

        public float Value { get; }

        public string Unit { get; }

        public void Deconstruct(out IntelPayloadType type, out float value, out string unit)
        {
            type = Type;
            value = Value;
            unit = Unit;
        }

        public bool Equals(IntelPayload other) =>
            Type == other.Type && Value.Equals(other.Value) && string.Equals(Unit, other.Unit, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is IntelPayload other && Equals(other);

        public override int GetHashCode() => (Type, Value, Unit).GetHashCode();

        public static bool operator ==(IntelPayload left, IntelPayload right) => left.Equals(right);

        public static bool operator !=(IntelPayload left, IntelPayload right) => !left.Equals(right);

        public override string ToString() => $"IntelPayload({Type}, {Value}, {Unit})";
    }

    /// <summary>
    /// 情报对象——接收方视角（情报规格 §8.1）。
    /// 权威端字段（true_content/deception）在 <see cref="AuthoritativeIntel"/> 分离建模（NET-02/SLICE-06），
    /// 接收方查询永远拿不到权威端块。
    /// </summary>
    public sealed class IntelRecord
    {
        public string IntelId { get; init; }

        /// <summary>情报主题，如 "赵军_西线兵力"；信任桶按此分桶（TRUST-01）。</summary>
        public string Topic { get; init; }

        public IntelSourceType SourceType { get; init; }

        /// <summary>来源基值（INTEL-01；规律归纳为生成时快照值，INTEL-10）。</summary>
        public float BaseCredibility { get; init; }

        public IntelPayload RawContent { get; init; }

        public int ScoutCount { get; init; }

        public float ObservationMinutes { get; init; }

        /// <summary>产生时刻（战役时钟，秒）。</summary>
        public float CreatedAtSeconds { get; init; }

        public IntelStatus Status { get; private set; } = IntelStatus.Active;

        /// <summary>新鲜可信度（产生时算一次；规律归纳条目此后只走年龄衰减，INTEL-10）。</summary>
        public float FreshCredibility { get; private set; }

        public float ExpirySeconds => CreatedAtSeconds + CredibilityConfig.Default.ExpiryMinutes * 60f;

        public void Initialize(float freshCredibility)
        {
            FreshCredibility = freshCredibility;
        }

        /// <summary>按战役时钟结算当前状态：满 15 分钟过期（INTEL-04）。</summary>
        public float Tick(float nowSeconds, CredibilityConfig? config = null)
        {
            var c = config ?? CredibilityConfig.Default;
            if (Status == IntelStatus.FlaggedFake)
            {
                return c.FlaggedFakeCredibility;
            }

            if (Status == IntelStatus.Active && nowSeconds >= ExpirySeconds)
            {
                Status = IntelStatus.Expired;
            }

            if (Status == IntelStatus.Expired)
            {
                // 过期后移出综合计算；返回地板值仅用于时间线展示。
                return CredibilityCalculator.Aged(FreshCredibility, BaseCredibility, c.ExpiryMinutes, c);
            }

            float ageMinutes = Math.Max(0f, (nowSeconds - CreatedAtSeconds) / 60f);
            return CredibilityCalculator.Aged(FreshCredibility, BaseCredibility, ageMinutes, c);
        }

        /// <summary>识破：可信度瞬间 15%、标疑似伪造、退出综合计算（INTEL-08/TRUST-06）。</summary>
        public void FlagAsFake()
        {
            Status = IntelStatus.FlaggedFake;
        }
    }

}