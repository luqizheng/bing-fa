using System.Collections.Generic;

namespace ChinaBettle.Battle.Campaign
{
    /// <summary>
    /// 教程步骤（计划 §3 P6-2 的五步引导）：
    /// 侦查 → 辨假灶 → 施计 → 识破 → 复盘。
    ///
    /// 顺序即认知顺序：先让玩家看见"情报从哪来"，再让他看见"情报可以是假的"，
    /// 然后让他亲手造假、亲手拆穿，最后回看整局——这正是 SLICE-05
    /// "欺骗技能改变了我的决策方式" 的教学路径。
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>未开始。</summary>
        None,

        /// <summary>① 派斥候侦查，等第一条目视情报。</summary>
        Scout,

        /// <summary>② 分辨真假灶（远观炊烟 vs 近距清点）。</summary>
        DiscernStove,

        /// <summary>③ 释放一次欺骗技能（减灶/增灶）。</summary>
        CastDeception,

        /// <summary>④ 识破一次敌方欺骗（近距清点或异质信源）。</summary>
        DetectDeception,

        /// <summary>⑤ 战后复盘（查看桶值轨迹与编年史）。</summary>
        Review,

        /// <summary>教程完成。</summary>
        Completed,
    }

    /// <summary>教程一步的规格：完成条件与提示文案。</summary>
    public sealed class TutorialStepSpec
    {
        public TutorialStepSpec(
            TutorialStep step,
            string title,
            string hint,
            System.Func<TutorialContext, bool> isDoneWhen)
        {
            Step = step;
            Title = title;
            Hint = hint;
            IsDoneWhen = isDoneWhen;
        }

        public TutorialStep Step { get; }

        public string Title { get; }

        /// <summary>玩家看到的操作提示（不暴露规则数值，只指路）。</summary>
        public string Hint { get; }

        public System.Func<TutorialContext, bool> IsDoneWhen { get; }
    }

    /// <summary>教程判定所需的只读快照（由仿真每步组装；不暴露敌方真值，NET-02）。</summary>
    public sealed class TutorialContext
    {
        /// <summary>玩家情报池里已有多少条活跃目视情报。</summary>
        public int ActiveScoutIntelCount { get; init; }

        /// <summary>玩家是否已获得过至少一条被篡改的载荷（假灶的"被看见"）。</summary>
        public bool SawTamperedPayload { get; init; }

        /// <summary>玩家是否已近距清点过（识破路径①的触发动作）。</summary>
        public bool DidProximityCheck { get; init; }

        /// <summary>玩家已成功施放欺骗技能次数。</summary>
        public int PlayerCastCount { get; init; }

        /// <summary>玩家已识破敌方欺骗次数（SKILL-11 的 +15 回收渠道）。</summary>
        public int PlayerDetectionCount { get; init; }

        /// <summary>战役是否已结算（复盘步骤的前提）。</summary>
        public bool BattleFinished { get; init; }
    }

    /// <summary>
    /// 教程引导运行器（纯逻辑，可单测）。
    ///
    /// 设计取舍：教程是**非阻塞**的——它只推进自己的步骤并给提示，
    /// 不改战场、不锁操作（玩家可以无视它自由玩）。这符合"不削弱核心循环"的纪律：
    /// 教程若强制顺序施计，反而会破坏"欺骗需要时机"的设计。
    /// </summary>
    public sealed class TutorialGuide
    {
        private readonly TutorialStepSpec[] steps;
        private int index;

        public TutorialGuide(TutorialStepSpec[]? steps = null)
        {
            this.steps = steps ?? BuildDefault();
        }

        public TutorialStep Current => index < steps.Length ? steps[index].Step : TutorialStep.Completed;

        public string CurrentTitle => index < steps.Length ? steps[index].Title : "教程完成";

        public string CurrentHint => index < steps.Length ? steps[index].Hint : "按 R 重开一局，或继续自由推演。";

        public bool IsCompleted => index >= steps.Length;

        /// <summary>步骤完成时触发一次的提示（供 HUD toast）。</summary>
        public string? LastAdvancedMessage { get; private set; }

        /// <summary>推进教程。返回本次是否推进了步骤。</summary>
        public bool Step(TutorialContext ctx)
        {
            LastAdvancedMessage = null;

            // 一帧内允许连锁推进会立即满足的步骤（如①侦查与②辨假灶可能同时成立）。
            bool advanced = false;
            while (index < steps.Length && steps[index].IsDoneWhen(ctx))
            {
                LastAdvancedMessage = $"教程：{steps[index].Title} 已完成";
                index++;
                advanced = true;
            }

            return advanced;
        }

        private static TutorialStepSpec[] BuildDefault() => new[]
        {
            new TutorialStepSpec(
                TutorialStep.Scout,
                "① 派遣斥候侦查",
                "选中斥候，派往河谷与敌营方向——情报只能靠侦查获得。",
                ctx => ctx.ActiveScoutIntelCount >= 1),

            new TutorialStepSpec(
                TutorialStep.DiscernStove,
                "② 分辨真假灶",
                "远观炊烟只能估算兵力；派斥候贴近营盘点数，才能看到真实规模。",
                ctx => ctx.SawTamperedPayload || ctx.DidProximityCheck),

            new TutorialStepSpec(
                TutorialStep.CastDeception,
                "③ 施放一次诡道",
                "按 F1/F2 释放减灶或增灶——欺骗是篡改敌方看到的载荷，不是直接扣数值。",
                ctx => ctx.PlayerCastCount >= 1),

            new TutorialStepSpec(
                TutorialStep.DetectDeception,
                "④ 识破敌方欺骗",
                "若敌方也对你施计：贴近清点，或让异质信源交叉验证。",
                ctx => ctx.PlayerDetectionCount >= 1),

            new TutorialStepSpec(
                TutorialStep.Review,
                "⑤ 战后复盘",
                "战斗结束后查看复盘：AI 的信任桶如何被养起、又如何被反转。",
                ctx => ctx.BattleFinished),
        };
    }
}
