using System.Collections.Generic;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 剧本运行器（纯逻辑）：按 <see cref="CampaignActSpec"/> 序列推进当前幕，产出事件流。
    ///
    /// 纪律：
    /// ① 本类**不持有也不修改**战场状态——输入是只读快照 <see cref="CampaignContext"/>，
    ///    输出是事件列表；执行的职责在 <c>BattleSimulation</c>。故可脱离 Unity/仿真单测。
    /// ② 每步最多推进若干幕（可连锁跨越，如换将幕瞬时完成），但**不会**在同一帧内重复触发同一幕效果。
    /// ③ 判定顺序：先看结局（CP-08），再看是否推进到下一幕。
    /// </summary>
    public sealed class CampaignRunner
    {
        private readonly CampaignActSpec[] acts;
        private CampaignAct current = CampaignAct.None;
        private bool started;
        private float actEnteredAtSeconds;

        public CampaignRunner(CampaignActSpec[]? acts = null)
        {
            this.acts = acts ?? ChangpingCampaign.Acts;
        }

        /// <summary>当前幕。</summary>
        public CampaignAct Current => current;

        /// <summary>当前幕规格（未开始时返回 null）。</summary>
        public CampaignActSpec? CurrentSpec => SpecOf(current);

        /// <summary>当前幕标题（HUD 用）。</summary>
        public string CurrentTitle => CurrentSpec?.Title ?? "未开始";

        /// <summary>是否已开始推进过。</summary>
        public bool HasStarted => started;

        /// <summary>已触发过的结局（未结束时为 null）。</summary>
        public CampaignEnding? Ending { get; private set; }

        /// <summary>幕历史（用于复盘：每幕进入的战役时刻与标题）。</summary>
        public List<(float AtSeconds, CampaignAct Act, string Title)> History { get; } = new();

        /// <summary>
        /// 推进一幕（**供测试与调试使用**；正常流程走 <see cref="Step"/>）。
        /// 与 <see cref="Step"/> 一致地写入 <see cref="History"/>——复盘要求幕历史完整，
        /// 不因推进路径不同而缺幕。
        /// </summary>
        public IReadOnlyList<CampaignEvent> AdvanceOneAct(CampaignContext ctx)
        {
            var events = new List<CampaignEvent>();

            if (!started)
            {
                started = true;
                current = acts[0].Act;
                actEnteredAtSeconds = ctx.ElapsedSeconds;
                History.Add((ctx.ElapsedSeconds, current, CurrentTitle));
                EnterAct(acts[0], ctx, events);
                return events;
            }

            int index = IndexOf(current);
            if (index < 0 || index >= acts.Length - 1)
            {
                return events;
            }

            ExitAct(acts[index], ctx, events);
            current = acts[index + 1].Act;
            actEnteredAtSeconds = ctx.ElapsedSeconds;
            History.Add((ctx.ElapsedSeconds, current, CurrentTitle));
            EnterAct(acts[index + 1], ctx, events);
            return events;
        }

        /// <summary>单步推进：返回本次产出的事件（可能跨多幕连锁）。</summary>
        public CampaignStepResult Step(CampaignContext ctx)
        {
            var events = new List<CampaignEvent>();

            // 回填"本幕已持续多久"——幕的推进条件可用它表达最短驻留时长。
            ctx.SecondsInAct = started ? ctx.ElapsedSeconds - actEnteredAtSeconds : 0f;

            if (Ending is not null)
            {
                return new CampaignStepResult(current, false, events);
            }

            if (!started)
            {
                started = true;
                current = acts[0].Act;
                actEnteredAtSeconds = ctx.ElapsedSeconds;
                History.Add((ctx.ElapsedSeconds, current, CurrentTitle));
                EnterAct(acts[0], ctx, events);
                return new CampaignStepResult(current, true, events);
            }

            // ① 结局优先（CP-08）。
            var ending = ChangpingCampaign.EvaluateEnding(ctx, current);
            if (ending is not null)
            {
                Ending = ending;
                current = CampaignAct.Finished;
                events.Add(new CampaignEnd
                {
                    Text = $"{ending.Id}：{ending.Narrative}",
                    IsHighlight = true,
                    EndingId = ending.Id,
                    PlayerWins = ending.PlayerWins,
                });
                return new CampaignStepResult(current, true, events);
            }

            // ② 幕推进（可连锁，但设上限防止空转）。
            bool advanced = false;
            for (int guard = 0; guard < acts.Length; guard++)
            {
                var spec = SpecOf(current);
                if (spec is null || !spec.AdvanceWhen(ctx))
                {
                    break;
                }

                int index = IndexOf(current);
                if (index < 0 || index >= acts.Length - 1)
                {
                    break;
                }

                ExitAct(spec, ctx, events);
                current = acts[index + 1].Act;
                actEnteredAtSeconds = ctx.ElapsedSeconds;
                History.Add((ctx.ElapsedSeconds, current, CurrentTitle));
                EnterAct(acts[index + 1], ctx, events);
                advanced = true;
            }

            return new CampaignStepResult(current, advanced, events);
        }

        private void EnterAct(CampaignActSpec spec, CampaignContext ctx, List<CampaignEvent> sink)
        {
            // 进入效果（文案 + 事件）。
            sink.Add(new CampaignActEntered
            {
                Text = $"{spec.Title}｜{spec.OpeningText}",
                IsHighlight = true,
                Act = spec.Act,
                Title = spec.Title,
                OpeningText = spec.OpeningText,
            });

            foreach (var e in spec.OnEnter)
            {
                sink.Add(e);
            }
        }

        private void ExitAct(CampaignActSpec spec, CampaignContext ctx, List<CampaignEvent> sink)
        {
            foreach (var e in spec.OnExit)
            {
                sink.Add(e);
            }
        }

        private int IndexOf(CampaignAct act)
        {
            for (int i = 0; i < acts.Length; i++)
            {
                if (acts[i].Act == act)
                {
                    return i;
                }
            }

            return -1;
        }

        private CampaignActSpec? SpecOf(CampaignAct act)
        {
            int i = IndexOf(act);
            return i < 0 ? null : acts[i];
        }
    }

    /// <summary>幕进入事件（供 HUD 显示幕名与叙事文案）。</summary>
    public sealed class CampaignActEntered : CampaignEvent
    {
        public CampaignAct Act { get; init; }

        public string Title { get; init; } = string.Empty;

        public string OpeningText { get; init; } = string.Empty;
    }

}
