namespace ChinaBettle.Foundation.AI;

/// <summary>
/// AI 欺骗行为参数（真源 AI-05）。技能消耗/CD 与玩家同规则，走 StratagemDefinition（30 点/90s），
/// 不在本配置重复。识破奖励 +15 走 StrategyPointConfig.DetectReward（SKILL-11）。
/// </summary>
public sealed record AiDeceptionConfig(
    /// <summary>兵力劣势判定：ai_strength &lt; 本系数 × player_strength → 增灶示强（AI-05①）。</summary>
    float DisadvantageStrengthRatio = 0.7f)
{
    public static AiDeceptionConfig Default { get; } = new();
}
