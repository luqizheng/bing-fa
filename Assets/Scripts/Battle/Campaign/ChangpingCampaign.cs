using System.Collections.Generic;
using System.Linq;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 长平之战剧本（真源 CP-01…08）。
    ///
    /// 结构（CP-02）：七幕 —— ①开局对峙 → ②挑战与拒战 → ③反间换将（战略层旬）→ ④赵军出垒
    /// → ⑤合围成形 → ⑥断粮围困 → ⑦突围与结局。
    ///
    /// 时间处理（CP-01，**不改 TIME-03/04**）：整场战役以战役层实时推进，一幕内的节拍由阈值驱动；
    /// 第 ③ 幕（换将）与第 ⑥ 幕的"围困日数"在叙事上属战略层旬推进，本文本以叙事文案表达，
    /// **不新增时间机制**、不做 46 日逐日模拟——饥饿按 FOOD-04/06 的分钟级累积实时表达。
    ///
    /// 阈值取值说明：下述时长为【拟设】内容参数，集中在本类顶部以便评审后回写真源 CP 系列。
    /// </summary>
    public static class ChangpingCampaign
    {
        // ── 幕推进阈值（【拟设】，待评审回写真源 CP 系列）──
        /// <summary>① 对峙时长：给双方斥候建立首批情报与"桶预养"的窗口。</summary>
        public const float StandoffSeconds = 60f;

        /// <summary>② 挑战时长：秦军挑战、赵军不出，使 AI 对"赵军固守"载荷的桶升过 0.8（TRUST-09 预养）。</summary>
        public const float ChallengeSeconds = 90f;

        /// <summary>⑤ 合围巩固时长：合围成形后就地转入封锁所需时间。</summary>
        public const float EncircleConsolidateSeconds = 60f;

        /// <summary>⑥ 围困触发突围的饥饿阈值（FOOD-04 三档的半程）。</summary>
        public const float SiegeStarvationTrigger = 0.6f;

        /// <summary>⑦ 突围判定窗口：进入突围后给玩家的反应时间。</summary>
        public const float BreakoutSeconds = 180f;

        /// <summary>CP-06③：出垒比例阈值——赵军出垒单位占初始兵力 60% 即触发合围判定。</summary>
        public const float SortieRatioThreshold = 0.60f;

        /// <summary>CP-06①：秦军占据粮道走廊所需的持续时长。</summary>
        public const float SupplyCorridorHoldSeconds = 30f;

        /// <summary>换将事件注入给赵方（玩家）的叙事载荷数值：【拟设】，表示"秦军可战之兵不足"的估算。</summary>
        public const float DismissalPayloadMen = 400f;

        /// <summary>胜负判定：赵军战斗单位被歼灭/崩溃到该比例即史实结局（CP-08①）。</summary>
        public const float RoutedRatioForHistoricalEnding = 0.80f;

        /// <summary>④ 出垒幕的最短持续时间：给赵军真正走出壁垒留出节拍，避免下令当帧即跳幕。</summary>
        public const float SortieSeconds = 100f;

        /// <summary>
        /// 出垒推进条件（CP-02 ④→⑤）。满足其一：
        /// ① 赵军越过壁垒比例达阈值（CP-06③，玩家操作下的自然路径）；
        /// ② 剧本已下令出垒**且**已给出垒动作留出 <see cref="SortieSeconds"/> 的时间
        ///    （无玩家操作的演示局路径——赵军的出击是史实动作，由剧本下令，不作为自主 AI 行为）。
        ///
        /// 同时要求"已下令"，是为了防止玩家尚未出垒时被剧本抢先推进。
        /// </summary>
        public static bool ShouldLeaveSortie(CampaignContext ctx)
        {
            if (SortieRatio(ctx) >= SortieRatioThreshold)
            {
                return true;
            }

            return ctx.ZhaoSortedOut && ctx.SecondsInAct >= SortieSeconds;
        }

        public static CampaignActSpec[] Acts { get; } = Build();

        private static CampaignActSpec[] Build()
        {
            return new[]
            {
                new CampaignActSpec(
                    CampaignAct.Standoff,
                    "① 开局对峙",
                    "赵军据西垒、凭百里石长城壁垒不出；秦军列于东垒，双方斥候散出侦查。",
                    advanceWhen: ctx => ctx.ElapsedSeconds >= StandoffSeconds,
                    onEnter: new List<CampaignEvent>(),
                    onExit: new List<CampaignEvent>()),

                new CampaignActSpec(
                    CampaignAct.Challenge,
                    "② 挑战与拒战",
                    "秦军数度挑战，赵军坚壁不出——【桶预养期】：常规侦查反复确认真实载荷，为后续欺骗铺垫（TRUST-09）。",
                    advanceWhen: ctx => ctx.ElapsedSeconds >= ChallengeSeconds,
                    onEnter: new List<CampaignEvent>(),
                    onExit: new List<CampaignEvent>()),

                new CampaignActSpec(
                    CampaignAct.Dismissal,
                    "③ 反间换将",
                    "秦相应侯范雎使人行反间于邯郸：「秦之所恶，独畏马服君赵奢之子赵括为将耳。」赵王遂以赵括代廉颇。",
                    // 换将为叙事事件：进入本幕即生效（不可施放/不可反制/无拒绝分支，真源 ADV-07 / SKILL-13）。
                    advanceWhen: ctx => true,
                    onEnter: new List<CampaignEvent>
                    {
                        new NarrativeIntelInject
                        {
                            Text = "反间得成：赵括代廉颇。赵军斥候回报「秦军可战之兵不足」——真伪并存，须自行判断。",
                            IsHighlight = true,
                            Recipient = Faction.Zhao,
                            Topic = "秦军_兵力",
                            SourceKind = IntelSourceType.CapturedDocument,
                            Value = DismissalPayloadMen,
                        },
                    },
                    onExit: new List<CampaignEvent>()),

                new CampaignActSpec(
                    CampaignAct.Sortie,
                    "④ 赵军出垒",
                    "赵括既代廉颇，悉更约束、易置军吏，下令出击——赵军离开壁垒，渡过丹水。",
                    advanceWhen: ShouldLeaveSortie,
                    onEnter: new List<CampaignEvent>
                    {
                        // 赵军出垒是史实动作，由剧本下令（而非等 AI 替赵军动）。
                        new SortieOrder
                        {
                            Text = "赵括悉更约束、易置军吏，出兵击秦师——赵军离开壁垒，渡过丹水。",
                            IsHighlight = true,
                        },
                        // 秦军佯败诱敌（CP-02 ④，白起"佯败而走"）：转入追击态势，把赵军引离壁垒。
                        new PursueOrder
                        {
                            Text = "白起阴使奇兵——秦军一部佯败退走，赵括悉众追之。",
                            IsHighlight = true,
                        },
                    },
                    onExit: new List<CampaignEvent>()),

                new CampaignActSpec(
                    CampaignAct.Encirclement,
                    "⑤ 合围成形",
                    "秦军奇兵绝赵军后路，骑兵割裂壁垒——赵军与西垒之间被切断（CP-06）。",
                    // 合围巩固：以**本幕驻留时长**衡量（不是全局时刻），否则会被后续幕提前吃掉。
                    advanceWhen: ctx => ctx.SecondsInAct >= EncircleConsolidateSeconds || ctx.QinCutSupplyCorridor,
                    onEnter: new List<CampaignEvent>
                    {
                        new EncircleOrder
                        {
                            Text = "合围：秦军两翼齐出，绝赵军后路，封锁粮道。",
                            IsHighlight = true,
                        },
                        new NarrativeIntelInject
                        {
                            Text = "秦军增灶示强、主力动向不明——赵军斥候回报的分歧正在扩大。",
                            Recipient = Faction.Zhao,
                            Topic = "秦军_兵力",
                            SourceKind = IntelSourceType.Prisoner,
                            Value = 1800f,
                        },
                    },
                    onExit: new List<CampaignEvent>()),

                new CampaignActSpec(
                    CampaignAct.Siege,
                    "⑥ 断粮围困",
                    "粮道既绝，赵军乏食——史载围困四十六日；本役以饥饿累积实时表达（FOOD-04/06）。",
                    // 围困到"饥饿过半"或粮仓失守 → 进入突围；否则撑到硬上限。
                    advanceWhen: ctx => ctx.ZhaoGranaryLost || ctx.ElapsedSeconds >= TimeLimitSeconds - BreakoutSeconds,
                    onEnter: new List<CampaignEvent>
                    {
                        new HoldOrder
                        {
                            Text = "秦军就地转入封锁，不再强攻——赵军粮尽，唯有突围一途。",
                            IsHighlight = true,
                        },
                    },
                    onExit: new List<CampaignEvent>()),

                new CampaignActSpec(
                    CampaignAct.Breakout,
                    "⑦ 突围与结局",
                    "赵军择向突围：或拼死冲出渡口，或困守西垒待援。",
                    advanceWhen: ctx => false, // 由 BattleSimulation 依 CP-08 结局条件结算
                    onEnter: new List<CampaignEvent>(),
                    onExit: new List<CampaignEvent>()),
            };
        }

        /// <summary>CP-06③：出垒比例＝壁垒外战斗单位 / 初始战斗单位。</summary>
        public static float SortieRatio(CampaignContext ctx) =>
            ctx.ZhaoInitialUnits <= 0 ? 0f : (float)ctx.ZhaoUnitsBeyondWall / ctx.ZhaoInitialUnits;

        // ───────────────────────── 结局判定（CP-08）─────────────────────────

        /// <summary>
        /// 判定本局是否已到结局，以及是哪种结局。返回 null 表示战役未结束、继续当前幕。
        ///
        /// ① 史实结局（赵军溃降）：主力被歼/崩溃达 RoutedRatioForHistoricalEnding，或围困中兵力归零。
        /// ② 改写结局（赵军撤退成功）：在突围中存在成建制脱离（突围窗口内仍有 ≥1 支存活主力）。
        /// </summary>
        public static CampaignEnding? EvaluateEnding(CampaignContext ctx, CampaignAct act)
        {
            if (ctx.ZhaoUnits <= 0)
            {
                return new CampaignEnding("史实结局·长平之败", PlayerWins: false,
                    "赵军主力尽墨，赵括死于乱军之中，全军溃降——长平之战以秦之大胜告终。");
            }

            if (ctx.QinUnits <= 0)
            {
                return new CampaignEnding("改写结局·秦军退却", PlayerWins: true,
                    "秦军主力被歼，白起退兵——长平之围遂解。");
            }

            float survivalRatio = ctx.ZhaoInitialUnits <= 0
                ? 0f
                : (float)ctx.ZhaoUnits / ctx.ZhaoInitialUnits;

            // 史实结局：主力损失达阈值（CP-08①）。
            // 限定在**合围之后**：史实中赵军是"被围绝粮而后溃"，而非出垒追击一触即溃；
            // 出垒阶段的战损不应提前定性为长平之败。
            if (act >= CampaignAct.Encirclement && survivalRatio <= 1f - RoutedRatioForHistoricalEnding)
            {
                return new CampaignEnding("史实结局·长平之败", PlayerWins: false,
                    $"赵军损失逾 {RoutedRatioForHistoricalEnding * 100f:0}%，突围未成，赵括战死，全军降秦。");
            }

            // 改写结局：突围窗口内成建制脱离（CP-08②）。
            if (act == CampaignAct.Breakout && ctx.ZhaoBrokeOut)
            {
                return new CampaignEnding("改写结局·突围成功", PlayerWins: true,
                    "赵军主力趁夜渡丹水突围，退保西垒——虽失地，然全军得还。");
            }

            // 硬上限（TIME-04）：仍被困则按史实结局收束。
            if (ctx.ElapsedSeconds >= ctx.TimeLimitSeconds)
            {
                if (act >= CampaignAct.Siege)
                {
                    return new CampaignEnding("史实结局·长平之败", PlayerWins: false,
                        "围困至时限，赵军粮尽援绝，遂降。");
                }

                // 未进入围困即到时：按"守住西垒"的改写方向收束（CP-08② 的防守分支）。
                return new CampaignEnding("改写结局·守住西垒", PlayerWins: true,
                    "至时限，赵军始终未离壁垒，秦军粮尽退兵——西垒得保。");
            }

            return null;
        }

        private static float TimeLimitSeconds => 20f * 60f;
    }

    /// <summary>结局判定结果（CP-08）。</summary>
    public sealed record CampaignEnding(string Id, bool PlayerWins, string Narrative);

}
