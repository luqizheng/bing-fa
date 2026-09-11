using System.Collections.Generic;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 战役幕（真源 CP-02 幕序）。幕是剧本推进的最小单位：每幕有进入条件、结束条件与进入/结束效果。
    /// </summary>
    public enum CampaignAct
    {
        /// <summary>未开始。</summary>
        None,

        /// <summary>① 开局对峙：双方散出斥候，侦查与桶预养期。</summary>
        Standoff,

        /// <summary>② 挑战与拒战：秦军挑战、赵军不出垒（桶继续预养至 &gt;0.8）。</summary>
        Challenge,

        /// <summary>③ 反间换将（战略层旬推进，非实时）：廉颇去职、赵括接任（CP-03）。</summary>
        Dismissal,

        /// <summary>④ 赵军出垒：追击"退却"的秦军，脱离壁垒。</summary>
        Sortie,

        /// <summary>⑤ 合围成形：秦军奇兵绝后路、割裂壁垒（CP-06）。</summary>
        Encirclement,

        /// <summary>⑥ 断粮围困：粮道被切断，饥饿累积（FOOD-06）。</summary>
        Siege,

        /// <summary>⑦ 突围与结局：赵军择向突围（CP-08）。</summary>
        Breakout,

        /// <summary>战役结束（已结算）。</summary>
        Finished,
    }

    /// <summary>秦军态势（由剧幕驱动，供 AI 态势决策消费；不替代 AI-01 情报主链路）。</summary>
    public enum AiPosture
    {
        /// <summary>守势：依托守势线固守（第 ①②幕）。</summary>
        Hold,

        /// <summary>攻势：主动前压（第 ④ 幕佯败诱敌后转入追击）。</summary>
        Offensive,

        /// <summary>合围：就地封锁、绝敌后路与粮道（第 ⑤⑥幕）。</summary>
        Encircle,
    }

    /// <summary>幕结束原因，用于编年史与结局判定（CP-08）。</summary>
    public enum ActOutcome
    {
        /// <summary>条件满足、正常推进。</summary>
        Completed,

        /// <summary>达 20 战场分钟硬上限（TIME-04）。</summary>
        TimedOut,

        /// <summary>任一方主力被歼灭。</summary>
        Annihilated,

        /// <summary>据点/粮仓被夺取或焚毁。</summary>
        StrongholdLost,
    }

    /// <summary>
    /// 剧本事件（剧本层唯一对外效果通道）。
    ///
    /// 纪律：剧本**不直接读写仿真内部状态**——它只产出事件，由 <c>BattleSimulation</c> 执行。
    /// 这样剧本可被纯逻辑单测（注入假状态即可），且未来可原样搬到权威服务端（NET-01/02）。
    /// </summary>
    public abstract class CampaignEvent
    {
        /// <summary>事件文案（写入编年史，GDD §5.4）。</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>是否为玩家可见的关键事件（HUD 弹出提示）。</summary>
        public bool IsHighlight { get; init; }
    }

    /// <summary>发动收官攻势：秦军主动向赵军推进（由仿真把赵军非斥候单位交由 AI 接管并压上）。</summary>
    public sealed class OffensiveOrder : CampaignEvent
    {
        /// <summary>AI 守势线/攻击轴目标（地图定义给出）。</summary>
        public MapPoint Target { get; init; }
    }

    /// <summary>
    /// 下令赵军出垒（CP-02 ④）：赵括代廉颇后主动出击，是史实动作——
    /// 由剧本下令，而非依赖玩家操作或 AI 替赵军行动（否则演示局会在第 ④ 幕死锁）。
    /// </summary>
    public sealed class SortieOrder : CampaignEvent
    {
        /// <summary>出垒后的推进轴目标（＝敌方守势线/本阵方向）。</summary>
        public MapPoint Target { get; init; }
    }

    /// <summary>转入守势：停止进攻、回撤到守势线（合围成形后秦军就地转入封锁）。</summary>
    public sealed class HoldOrder : CampaignEvent
    {
        /// <summary>守势线坐标。</summary>
        public MapPoint Line { get; init; }
    }

    /// <summary>合围：秦军进入封锁态势（就地据守 + 封锁粮道，触发 FOOD-06 判定）。</summary>
    public sealed class EncircleOrder : CampaignEvent
    {
        /// <summary>合围圈中心（渡口一线）。</summary>
        public MapPoint Center { get; init; }
    }

    /// <summary>
    /// 剧本事件载荷：向某方情报池注入一条**叙事载荷**（如换将时的"秦军畏赵括"）。
    ///
    /// 与欺骗技能的边界（v1.3 真源 SKILL-13）：本事件**不是**欺骗技能产出、不走 DECP-01 的
    /// 篡改系数、不产生痕迹、不可被识破（它是史实叙事事实，而非伪造情报）。
    /// 载荷按正常可信度公式（§3.1）与权重（§3.2）参与综合计算。
    /// </summary>
    public sealed class NarrativeIntelInject : CampaignEvent
    {
        /// <summary>接收方（谁的情报池收到这条载荷）。</summary>
        public Faction Recipient { get; init; }

        /// <summary>情报主题（信任桶分桶键，TRUST-01）。</summary>
        public string Topic { get; init; } = string.Empty;

        /// <summary>来源类型（决定基值与权重，INTEL-01/05）。</summary>
        public IntelSourceType SourceKind { get; init; }

        /// <summary>载荷数值（兵力估算口径，单位"人"）。</summary>
        public float Value { get; init; }
    }

    /// <summary>剧本宣告战役结束（CP-08 结局分支）。</summary>
    public sealed class CampaignEnd : CampaignEvent
    {
        /// <summary>结局标识（"史实结局"/"改写结局"）。</summary>
        public string EndingId { get; init; } = string.Empty;

        /// <summary>玩家（赵）是否取胜。</summary>
        public bool PlayerWins { get; init; }
    }

    /// <summary>幕的规格：进入条件、推进条件、进入与结束的效果。</summary>
    public sealed class CampaignActSpec
    {
        public CampaignActSpec(
            CampaignAct act,
            string title,
            string openingText,
            System.Func<CampaignContext, bool> advanceWhen,
            IReadOnlyList<CampaignEvent> onEnter,
            IReadOnlyList<CampaignEvent> onExit)
        {
            Act = act;
            Title = title;
            OpeningText = openingText;
            AdvanceWhen = advanceWhen;
            OnEnter = onEnter;
            OnExit = onExit;
        }

        public CampaignAct Act { get; }

        /// <summary>幕名（HUD 与编年史）。</summary>
        public string Title { get; }

        /// <summary>进入本幕时写入编年史的叙事文案。</summary>
        public string OpeningText { get; }

        /// <summary>推进到下一幕的条件（纯函数，输入只读上下文）。</summary>
        public System.Func<CampaignContext, bool> AdvanceWhen { get; }

        public IReadOnlyList<CampaignEvent> OnEnter { get; }

        public IReadOnlyList<CampaignEvent> OnExit { get; }
    }

    /// <summary>
    /// 剧本推进所需的**只读**战场快照（由 <c>BattleSimulation</c> 每步组装）。
    /// 剧本只看这些量，不看仿真内部集合，保证可单测与可移植。
    /// </summary>
    public sealed class CampaignContext
    {
        public float ElapsedSeconds { get; init; }

        public float TimeLimitSeconds { get; init; }

        /// <summary>赵军存活战斗单位数（不含斥候）。</summary>
        public int ZhaoUnits { get; init; }

        /// <summary>秦军存活战斗单位数（不含斥候）。</summary>
        public int QinUnits { get; init; }

        /// <summary>赵军初始战斗单位数（用于计算出垒比例，CP-06③）。</summary>
        public int ZhaoInitialUnits { get; init; }

        /// <summary>位于壁垒线【己方一侧以外】的赵军战斗单位数（＝已出垒，CP-06③）。</summary>
        public int ZhaoUnitsBeyondWall { get; init; }

        /// <summary>秦军是否已占据丹水渡口（CP-06②）。</summary>
        public bool QinHoldsFord { get; init; }

        /// <summary>秦军是否已抵达赵军粮道走廊并保持占据达到阈值（CP-06①）。</summary>
        public bool QinCutSupplyCorridor { get; init; }

        /// <summary>赵方任一粮仓是否已焚毁（CP-08 / FOOD-03）。</summary>
        public bool ZhaoGranaryLost { get; init; }

        /// <summary>赵方本阵是否失守。</summary>
        public bool ZhaoCampLost { get; init; }

        /// <summary>AI 对"赵军_兵力"主题的信任桶值（TRUST-01；仅内部使用，不暴露给玩家）。</summary>
        public float AiTrustOnZhao { get; init; }

        /// <summary>赵方（玩家）对"秦军_兵力"主题的信任桶值。</summary>
        public float PlayerTrustOnQin { get; init; }

        /// <summary>赵军主力是否已突围成功（第 ⑦ 幕判定用）。</summary>
        public bool ZhaoBrokeOut { get; init; }

        /// <summary>剧本是否已下令赵军出垒（CP-02 ④；出垒本身是史实动作，由剧本下令）。</summary>
        public bool ZhaoSortedOut { get; init; }

        /// <summary>当前幕已持续的战场秒数（由运行器回填；幕推进条件可用它表达"最短驻留时长"）。</summary>
        public float SecondsInAct { get; set; }
    }

    /// <summary>剧本单步推进的结果。</summary>
    public sealed class CampaignStepResult
    {
        public CampaignStepResult(CampaignAct act, bool advanced, IReadOnlyList<CampaignEvent> events)
        {
            Act = act;
            Advanced = advanced;
            Events = events;
        }

        /// <summary>本步结束时的当前幕。</summary>
        public CampaignAct Act { get; }

        /// <summary>本步是否发生了推进（含一次跨多幕的连锁推进）。</summary>
        public bool Advanced { get; }

        /// <summary>本步产出的全部事件（含被跳过幕的进入/结束效果）。</summary>
        public IReadOnlyList<CampaignEvent> Events { get; }
    }

}
