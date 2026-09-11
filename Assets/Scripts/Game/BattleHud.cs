using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Intel;
using UnityEngine;
using UnityEngine.UI;

namespace ChinaBettle.Game
{
    /// <summary>技能槽展示信息（HUD 只渲染，不判规则）。</summary>
    public sealed record SkillSlotInfo(string Key, string Name, int Cost, string State, bool Ready);

    /// <summary>幕僚建议行（TRUST-10 代理信号文案）。</summary>
    public sealed record AdvisorLine(string Name, string Trait, string Advice, bool Highlight);

    /// <summary>HUD 每帧输入模型：由 <see cref="SliceGame"/> 组装，HUD 只负责画。</summary>
    public sealed class HudModel
    {
        public float ElapsedSeconds { get; init; }

        public float RemainingSeconds { get; init; }

        public int Points { get; init; }

        public bool Paused { get; init; }

        public bool Finished { get; init; }

        public string OutcomeTitle { get; init; } = string.Empty;

        public string OutcomeReason { get; init; } = string.Empty;

        public string StatsLine { get; init; } = string.Empty;

        public IReadOnlyList<IntelFeedItem> Feed { get; init; } = new List<IntelFeedItem>();

        public IReadOnlyList<ChronicleEntry> Chronicle { get; init; } = new List<ChronicleEntry>();

        public IReadOnlyList<SkillSlotInfo> Skills { get; init; } = new List<SkillSlotInfo>();

        public IReadOnlyList<string> SelectedLines { get; init; } = new List<string>();

        public string AimPrompt { get; init; } = string.Empty;

        public IReadOnlyList<AdvisorLine> Advisors { get; init; } = new List<AdvisorLine>();

        public string GroupHint { get; init; } = string.Empty;
    }

    /// <summary>
    /// 战役 HUD（uGUI，全部代码构建，无预制体/美术资源）。
    /// 对应 SLICE-03④（情报三色条 + "？"标记）与 UI-01/02/03（键位提示、情报栏文案、伪造标记）。
    /// </summary>
    public sealed class BattleHud
    {
        private readonly Font font = SliceFont.Resolve();
        private readonly Text topLeft;
        private readonly Text topRight;
        private readonly Text topCenter;
        private readonly Text intelBody;
        private readonly Text advisorBody;
        private readonly Text chronicleBody;
        private readonly Text selectedBody;
        private readonly Text hintText;
        private readonly Text aimText;
        private readonly RectTransform aimPanel;
        private readonly RectTransform outcomePanel;
        private readonly Text outcomeTitle;
        private readonly Text outcomeBody;
        private readonly List<(Image Image, Text Text)> skillSlots = new();

        public BattleHud(Transform root)
        {
            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root, worldPositionStays: false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = (RectTransform)canvasGo.transform;

            // 顶栏
            var topBar = Panel(canvasRect, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 88f));
            topLeft = Label(topBar, "TopLeft", 26, TextAnchor.MiddleLeft, SlicePalette.TextPrimary);
            topLeft.rectTransform.anchorMin = new Vector2(0f, 0f);
            topLeft.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            topLeft.rectTransform.offsetMin = new Vector2(20f, 0f);
            topLeft.rectTransform.offsetMax = Vector2.zero;

            topCenter = Label(topBar, "TopCenter", 20, TextAnchor.MiddleCenter, SlicePalette.TextPrimary);
            topCenter.rectTransform.anchorMin = new Vector2(0.30f, 0f);
            topCenter.rectTransform.anchorMax = new Vector2(0.80f, 1f);
            topCenter.rectTransform.offsetMin = Vector2.zero;
            topCenter.rectTransform.offsetMax = Vector2.zero;

            topRight = Label(topBar, "TopRight", 26, TextAnchor.MiddleRight, SlicePalette.TextPrimary);
            topRight.rectTransform.anchorMin = new Vector2(0.72f, 0f);
            topRight.rectTransform.anchorMax = new Vector2(1f, 1f);
            topRight.rectTransform.offsetMin = Vector2.zero;
            topRight.rectTransform.offsetMax = new Vector2(-20f, 0f);

            // 技能槽（顶栏下方，居中）
            var skillBar = Panel(canvasRect, "SkillBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -88f), new Vector2(0f, 78f));
            for (int i = 0; i < 4; i++)
            {
                var slot = Panel(skillBar, "Slot" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 300f, 0f), new Vector2(288f, 70f));
                var label = Label(slot, "SlotText", 20, TextAnchor.MiddleCenter, SlicePalette.TextPrimary);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(6f, 2f);
                label.rectTransform.offsetMax = new Vector2(-6f, -2f);
                skillSlots.Add((slot.GetComponent<Image>(), label));
            }

            // 情报栏（左上）
            var intelPanel = Panel(canvasRect, "IntelPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -180f), new Vector2(600f, 470f));
            var intelTitle = Label(intelPanel, "IntelTitle", 24, TextAnchor.UpperLeft, SlicePalette.TextPrimary);
            intelTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            intelTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            intelTitle.rectTransform.offsetMin = new Vector2(16f, -46f);
            intelTitle.rectTransform.offsetMax = new Vector2(-16f, -10f);
            intelTitle.text = "情报栏（斥候目视 → 可信度三色 + 伪造标记）";
            intelBody = Label(intelPanel, "IntelBody", 19, TextAnchor.UpperLeft, SlicePalette.TextPrimary);
            intelBody.rectTransform.anchorMin = Vector2.zero;
            intelBody.rectTransform.anchorMax = Vector2.one;
            intelBody.rectTransform.offsetMin = new Vector2(16f, 12f);
            intelBody.rectTransform.offsetMax = new Vector2(-16f, -52f);

            // 幕僚建议（右上）
            var advisorPanel = Panel(canvasRect, "AdvisorPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -180f), new Vector2(470f, 300f));
            var advisorTitle = Label(advisorPanel, "AdvisorTitle", 24, TextAnchor.UpperLeft, SlicePalette.TextPrimary);
            advisorTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            advisorTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            advisorTitle.rectTransform.offsetMin = new Vector2(16f, -46f);
            advisorTitle.rectTransform.offsetMax = new Vector2(-16f, -10f);
            advisorTitle.text = "幕僚建议（按 1/2 查看，不暴露敌方信任桶）";
            advisorBody = Label(advisorPanel, "AdvisorBody", 19, TextAnchor.UpperLeft, SlicePalette.TextPrimary);
            advisorBody.rectTransform.anchorMin = Vector2.zero;
            advisorBody.rectTransform.anchorMax = Vector2.one;
            advisorBody.rectTransform.offsetMin = new Vector2(16f, 12f);
            advisorBody.rectTransform.offsetMax = new Vector2(-16f, -52f);

            // 编年史（右下）
            var chroniclePanel = Panel(canvasRect, "ChroniclePanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 92f), new Vector2(620f, 320f));
            var chronicleTitle = Label(chroniclePanel, "ChronicleTitle", 22, TextAnchor.UpperLeft, SlicePalette.TextMuted);
            chronicleTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            chronicleTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            chronicleTitle.rectTransform.offsetMin = new Vector2(16f, -42f);
            chronicleTitle.rectTransform.offsetMax = new Vector2(-16f, -8f);
            chronicleTitle.text = "编年史（GDD §5.4）";
            chronicleBody = Label(chroniclePanel, "ChronicleBody", 18, TextAnchor.UpperLeft, SlicePalette.TextMuted);
            chronicleBody.rectTransform.anchorMin = Vector2.zero;
            chronicleBody.rectTransform.anchorMax = Vector2.one;
            chronicleBody.rectTransform.offsetMin = new Vector2(16f, 10f);
            chronicleBody.rectTransform.offsetMax = new Vector2(-16f, -46f);

            // 选中单位（左下）
            var selectedPanel = Panel(canvasRect, "SelectedPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 92f), new Vector2(600f, 320f));
            var selectedTitle = Label(selectedPanel, "SelectedTitle", 22, TextAnchor.UpperLeft, SlicePalette.TextMuted);
            selectedTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            selectedTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            selectedTitle.rectTransform.offsetMin = new Vector2(16f, -42f);
            selectedTitle.rectTransform.offsetMax = new Vector2(-16f, -8f);
            selectedTitle.text = "选中部队（左键选、右键移动/攻击、G 结阵）";
            selectedBody = Label(selectedPanel, "SelectedBody", 19, TextAnchor.UpperLeft, SlicePalette.TextPrimary);
            selectedBody.rectTransform.anchorMin = Vector2.zero;
            selectedBody.rectTransform.anchorMax = Vector2.one;
            selectedBody.rectTransform.offsetMin = new Vector2(16f, 10f);
            selectedBody.rectTransform.offsetMax = new Vector2(-16f, -46f);

            // 底部键位提示
            var hintBar = Panel(canvasRect, "HintBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(0f, 68f));
            hintText = Label(hintBar, "HintText", 19, TextAnchor.MiddleCenter, SlicePalette.TextPrimary);
            hintText.rectTransform.anchorMin = Vector2.zero;
            hintText.rectTransform.anchorMax = Vector2.one;
            hintText.rectTransform.offsetMin = new Vector2(16f, 0f);
            hintText.rectTransform.offsetMax = new Vector2(-16f, 0f);
            hintText.text =
                "F1 减灶示弱 · F2 增灶示强 · F3 火攻粮道 | 左键选择 / 右键移动或攻击 | G 结阵 · H 停止 | " +
                "Ctrl+数字 编队 / Alt+数字 召回 | 空格 暂停 · R 重开 · WASD 平移 · 滚轮缩放";

            // 施计瞄准提示
            aimPanel = Panel(canvasRect, "AimPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 160f), new Vector2(860f, 70f));
            aimText = Label(aimPanel, "AimText", 22, TextAnchor.MiddleCenter, SlicePalette.TierDoubtful);
            aimText.rectTransform.anchorMin = Vector2.zero;
            aimText.rectTransform.anchorMax = Vector2.one;
            aimText.rectTransform.offsetMin = new Vector2(10f, 0f);
            aimText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            aimPanel.gameObject.SetActive(false);

            // 结算面板
            outcomePanel = Panel(canvasRect, "OutcomePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 640f));
            outcomeTitle = Label(outcomePanel, "OutcomeTitle", 42, TextAnchor.UpperCenter, SlicePalette.TextPrimary);
            outcomeTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            outcomeTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            outcomeTitle.rectTransform.offsetMin = new Vector2(20f, -84f);
            outcomeTitle.rectTransform.offsetMax = new Vector2(-20f, -20f);
            outcomeBody = Label(outcomePanel, "OutcomeBody", 21, TextAnchor.UpperLeft, SlicePalette.TextPrimary);
            outcomeBody.rectTransform.anchorMin = Vector2.zero;
            outcomeBody.rectTransform.anchorMax = Vector2.one;
            outcomeBody.rectTransform.offsetMin = new Vector2(28f, 24f);
            outcomeBody.rectTransform.offsetMax = new Vector2(-28f, -92f);
            outcomePanel.gameObject.SetActive(false);
        }

        public void Refresh(HudModel model)
        {
            topLeft.text =
                $"战役时间 {FormatTime(model.ElapsedSeconds)}　剩余 {FormatTime(model.RemainingSeconds)}" +
                (model.Paused ? "　【已暂停】" : string.Empty);

            topRight.text = $"谋略点 {model.Points} / 100";

            for (int i = 0; i < skillSlots.Count; i++)
            {
                var (image, text) = skillSlots[i];
                if (i < model.Skills.Count)
                {
                    var slot = model.Skills[i];
                    text.text = $"[{slot.Key}] {slot.Name}\n{slot.Cost} 点 · {slot.State}";
                    image.color = slot.Ready ? SlicePalette.SkillReady : SlicePalette.SkillCooling;
                }
                else
                {
                    text.text = $"[F{i + 1}] ——\n空槽";
                    image.color = SlicePalette.SkillCooling;
                }
            }

            intelBody.text = BuildIntelText(model);
            advisorBody.text = BuildAdvisorText(model);
            chronicleBody.text = BuildChronicleText(model);
            selectedBody.text = model.SelectedLines.Count == 0
                ? "（未选中）\n左键点选部队；Ctrl+数字 存编队，Alt+数字 召回"
                : string.Join("\n", model.SelectedLines);

            aimPanel.gameObject.SetActive(!string.IsNullOrEmpty(model.AimPrompt));
            aimText.text = model.AimPrompt;

            outcomePanel.gameObject.SetActive(model.Finished);
            if (model.Finished)
            {
                outcomeTitle.text = model.OutcomeTitle;
                outcomeBody.text = model.OutcomeReason + "\n" + model.StatsLine +
                                   "\n\n—— 编年史（末 8 条）——\n" + Tail(model.Chronicle, 8) +
                                   "\n\n按 R 重开一局";
            }

            topCenter.text = model.GroupHint;
        }

        private static string BuildIntelText(HudModel model)
        {
            if (model.Feed.Count == 0)
            {
                return "（暂无情报：派斥候接近敌军，目视才产情报）";
            }

            var builder = new StringBuilder();
            foreach (var item in model.Feed.Take(9))
            {
                string tier = TierName(item.Tier);
                string stamp = item.ShowFakeStamp ? "  〔？疑似伪造〕" : string.Empty;
                builder.AppendLine(
                    $"[{tier}] {SourceName(item.SourceType)}　{item.Topic} 约 {item.Value:0} 人" +
                    $"　可信度 {item.Credibility:0}%{stamp}");
            }

            return builder.ToString();
        }

        private static string BuildAdvisorText(HudModel model)
        {
            if (model.Advisors.Count == 0)
            {
                return "（暂无建议）";
            }

            var builder = new StringBuilder();
            foreach (var advisor in model.Advisors)
            {
                builder.AppendLine($"{(advisor.Highlight ? "▶ " : "　")}{advisor.Name}（{advisor.Trait}）：{advisor.Advice}");
            }

            builder.AppendLine();
            builder.Append("※ 幕僚只给时机判断，不显示敌方信任桶数值（TRUST-10）");
            return builder.ToString();
        }

        private static string BuildChronicleText(HudModel model) => Tail(model.Chronicle, 9);

        private static string Tail(IReadOnlyList<ChronicleEntry> entries, int count)
        {
            var tail = entries.Count <= count ? entries : entries.Skip(entries.Count - count).ToList();
            return string.Join("\n", tail.Select(e => $"{e.TimeLabel} {e.Text}"));
        }

        private static string TierName(CredibilityTier tier) => tier switch
        {
            CredibilityTier.Confident => "确信",
            CredibilityTier.Trusted => "可信",
            CredibilityTier.Doubtful => "存疑",
            _ => "不可信",
        };

        private static string SourceName(IntelSourceType source) => source switch
        {
            IntelSourceType.ScoutVisual => "斥候目视",
            IntelSourceType.Prisoner => "降兵口供",
            IntelSourceType.CapturedDocument => "截获文书",
            IntelSourceType.SkillReveal => "技惊暴露",
            _ => "规律归纳",
        };

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, (int)seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        // ───────────────────────────── 构建工具 ─────────────────────────────

        private RectTransform Panel(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = SlicePalette.PanelBackground;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, worldPositionStays: false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }

}