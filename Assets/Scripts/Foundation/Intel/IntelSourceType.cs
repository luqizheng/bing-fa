namespace ChinaBettle.Foundation.Intel;

/// <summary>
/// 情报来源类型（情报规格 §2）。
/// </summary>
public enum IntelSourceType
{
    /// <summary>斥候目视，基值 65%（INTEL-01）。</summary>
    ScoutVisual,

    /// <summary>降兵/乡民口供，基值 35%（INTEL-01）。</summary>
    Prisoner,

    /// <summary>截获文书，基值 80%（INTEL-01）。</summary>
    CapturedDocument,

    /// <summary>敌方技能暴露（如火攻浓烟），基值 75%，不可伪造（INTEL-01）。</summary>
    SkillReveal,

    /// <summary>长期规律归纳，基值=历史一致率 clamp 40–75%，生成时快照（INTEL-01/INTEL-10）。</summary>
    PatternInference,
}

/// <summary>情报状态（情报规格 §8.3）。</summary>
public enum IntelStatus
{
    /// <summary>活跃：参与同主题综合可信度计算。</summary>
    Active,

    /// <summary>过期：年龄满 15 分钟，移出活跃情报池（INTEL-04）。</summary>
    Expired,

    /// <summary>被识破的伪造情报：可信度固定 15%，退出综合计算（INTEL-08）。</summary>
    FlaggedFake,
}
