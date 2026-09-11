using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>
    /// 占位表现配色（无美术资源，纯程序化）。集中一处便于后续替换为正式美术。
    /// </summary>
    public static class SlicePalette
    {
        // 阵营：赵（玩家）青蓝、秦（AI）赤红
        public static readonly Color ZhaoUnit = new(0.36f, 0.60f, 0.85f);
        public static readonly Color QinUnit = new(0.78f, 0.24f, 0.22f);
        public static readonly Color ZhaoProp = new(0.30f, 0.52f, 0.72f);
        public static readonly Color QinProp = new(0.66f, 0.22f, 0.20f);

        public static readonly Color Ground = new(0.30f, 0.34f, 0.26f);
        public static readonly Color DifficultTerrain = new(0.20f, 0.27f, 0.19f);
        public static readonly Color Burned = new(0.15f, 0.13f, 0.12f);

        public static readonly Color Health = new(0.35f, 0.78f, 0.35f);
        public static readonly Color HealthLow = new(0.85f, 0.30f, 0.25f);
        public static readonly Color Morale = new(0.90f, 0.80f, 0.35f);
        public static readonly Color MoraleRouted = new(0.85f, 0.25f, 0.25f);
        public static readonly Color Formation = new(0.55f, 0.75f, 0.95f, 0.55f);
        public static readonly Color Selection = new(0.95f, 0.92f, 0.55f, 0.65f);
        public static readonly Color Fire = new(0.95f, 0.42f, 0.12f, 0.45f);

        // UI
        public static readonly Color PanelBackground = new(0.05f, 0.06f, 0.08f, 0.78f);
        public static readonly Color PanelBorder = new(0.35f, 0.38f, 0.42f, 0.9f);
        public static readonly Color TextPrimary = new(0.93f, 0.94f, 0.92f);
        public static readonly Color TextMuted = new(0.66f, 0.68f, 0.70f);

        /// <summary>情报档位三色（SLICE-03④：UI 三色条）。</summary>
        public static readonly Color TierConfident = new(0.45f, 0.85f, 0.95f);
        public static readonly Color TierTrusted = new(0.45f, 0.85f, 0.50f);
        public static readonly Color TierDoubtful = new(0.95f, 0.82f, 0.35f);
        public static readonly Color TierUntrusted = new(0.85f, 0.35f, 0.32f);

        public static readonly Color SkillReady = new(0.30f, 0.62f, 0.45f);
        public static readonly Color SkillCooling = new(0.32f, 0.33f, 0.35f);
        public static readonly Color SkillBlocked = new(0.42f, 0.26f, 0.26f);
    }

}