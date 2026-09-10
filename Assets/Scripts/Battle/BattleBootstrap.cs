using ChinaBettle.Foundation.Time;

namespace ChinaBettle.Battle;

/// <summary>
/// 单场战役引导（占位）。职责：组装战役时钟、双方情报池/信任系统、单位、
/// 三个 MVP 诡道技能（减灶/增灶/火攻）与 AI 行为树。具体接线在垂直切片迭代中补全。
/// 单局硬上限 20 战场分钟（TIME-04），到时按 §2.7.1 胜负条件结算。
/// </summary>
public sealed class BattleBootstrap
{
    private readonly BattleClock clock = new();

    public BattleClock Clock => clock;

    public void Tick(float deltaRealSeconds)
    {
        clock.Advance(deltaRealSeconds);
        // TODO(SLICE): 情报池 Tick → 信任桶/门控 → 行为树 → 单位与技能系统
    }
}
