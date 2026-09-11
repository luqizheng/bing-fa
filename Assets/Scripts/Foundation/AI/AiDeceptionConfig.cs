
namespace ChinaBettle.Foundation.AI
{
    /// <summary>
    /// AI 欺骗行为参数（真源 AI-05）与**欺骗战果转化**参数（真源 AI-06）。
    /// 技能消耗/CD 与玩家同规则，走 StratagemDefinition（30 点/90s），不在本配置重复。
    /// 识破奖励 +15 走 StrategyPointConfig.DetectReward（SKILL-11）。
    /// </summary>
    public sealed record AiDeceptionConfig(
        /// <summary>兵力劣势判定：ai_strength &lt; 本系数 × player_strength → 增灶示强（AI-05①）。</summary>
        float DisadvantageStrengthRatio = 0.7f,

        // ── AI-06 欺骗战果转化（真实兵力抽调，v1.4 新增；值均为【初值】待实机验证） ──

        /// <summary>
        /// 抽调比例：被欺骗方从**其它方向**抽调走多少比例的可用兵力前往载荷指向方向。
        /// 这是"欺骗产生战果"的关键——抽调造成原方向可观测、可打击的兵力空档。
        /// </summary>
        float DiversionRatio = 0.4f,

        /// <summary>
        /// 空档维持时长（战场秒）：从采信载荷到补位填回之间，抽调方向至少维持的战斗力缺口窗口。
        /// 给施计方创造可打击窗口（真源 AI-06⑤）。
        /// </summary>
        float WindowSeconds = 90f,

        /// <summary>
        /// 抽调的最小触发兵力：少于该单位数的方向不抽调（避免把守备掏空到荒唐）。
        /// </summary>
        int MinUnitsToDivert = 2,

        /// <summary>
        /// 空档可观测阈值：抽调后原方向单位数相对抽调前的下降比例达到该值即视为"空档成立"。
        /// 验收（AI-06）用它与 CP-09 判定。
        /// </summary>
        float VacancyThreshold = 0.3f)
    {
        public static AiDeceptionConfig Default { get; } = new();
    }

}
