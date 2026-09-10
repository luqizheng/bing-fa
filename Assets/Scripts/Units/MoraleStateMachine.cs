using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Units;

/// <summary>
/// 士气变化（真源 COMBAT-10）：主将阵亡 −30、被克制兵种命中 −5/次、战斗胜利 +10。
/// 断粮导致的士气变化【不在这里】，统一走饥饿度路径（FOOD-04，>80% 每 60 秒 −10），消除双轨扣减。
/// COMBAT-10 "幕僚亲临 +15" 为 v1.1 悬空项（幕僚无战场地图实体），未定案前不得实现。
/// </summary>
public sealed class MoraleStateMachine
{
    private readonly CombatConfig config;
    private int morale;

    public MoraleStateMachine(int startMorale = 50, CombatConfig? config = null)
    {
        this.config = config ?? CombatConfig.Default;
        morale = startMorale;
    }

    public int Value => morale;

    public MoraleState State => Morale.Classify(morale);

    public void OnCommanderKilled() => Adjust(-30);

    public void OnHitByCounterUnit() => Adjust(-5);

    public void OnBattleWon() => Adjust(+10);

    /// <summary>断粮士气扣减唯一入口（FOOD-04）。</summary>
    public void OnStarvationTick() => Adjust(config is null ? -10 : -10);

    private void Adjust(int delta)
    {
        morale = System.Math.Clamp(morale + delta, 0, 100);
    }
}
