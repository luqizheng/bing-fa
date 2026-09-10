namespace ChinaBettle.Foundation.Time;

/// <summary>
/// 双层时间模型归属（真源 TIME-01/TIME-02）。
/// 所有计时系统必须显式声明挂载层，禁止无归属的"回合/日"。
/// </summary>
public enum TimeLayer
{
    /// <summary>战略层：旬，回合制（外交/间谍/谋略点恢复/战略 CD/补给采购）。</summary>
    Strategic,

    /// <summary>战役层：秒，实时可暂停（移动战斗/技能战场 CD/情报采样与过期/复核/断粮）。</summary>
    Battle,
}
