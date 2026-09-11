using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>
    /// 战役表现层统一配色。对齐《[美术风格基线 v0.1](../Docs/美术风格基线_v0.1.md)》§2/§5。
    /// 真源 ID 标注于各字段注释：色码为美术规范独占（如需变更须同步本类与美术风格基线文档）。
    /// </summary>
    public static class SlicePalette
    {
        // ─────────── 三基色（覆盖 ≥80% 画面，§2.1） ───────────

        /// <summary>土黄 #c8a96a — 大地、皮革、纸张、单位底色（枪兵/矛阵）。</summary>
        public static readonly Color EarthYellow = new(0.784f, 0.663f, 0.416f);

        /// <summary>墨黑 #2a2620 — 文字、轮廓、阴影、单位底色（重步）。</summary>
        public static readonly Color InkBlack = new(0.165f, 0.149f, 0.125f);

        /// <summary>朱红 #a8412b — 强调、秦方标识、危险标记、单位底色（弩兵）。</summary>
        public static readonly Color Vermilion = new(0.659f, 0.255f, 0.169f);

        // ─────────── 辅助色（克制使用，§2.2） ───────────

        /// <summary>赭石 #8b6f47 — 木质、旗帜、幕僚色、单位底色（轻骑）、信任桶 0.70–1.00（真源 TRUST-09）。</summary>
        public static readonly Color Ochre = new(0.545f, 0.435f, 0.278f);

        /// <summary>石青 #3a5f5f — 链接、辅助文字、情报档位"可信"色（真源 INTEL-07）。</summary>
        public static readonly Color StoneBlue = new(0.227f, 0.373f, 0.373f);

        /// <summary>灰青 #4a5f5f — 假情报对象标记（真源 DECP-01/02/03，L1 §2.2.2）。</summary>
        public static readonly Color GreyTeal = new(0.290f, 0.373f, 0.373f);

        /// <summary>深墨绿 #1a2a1a — 未知迷雾蒙版（真源 INTEL-06，L1 §2.3.1）。</summary>
        public static readonly Color DeepMossGreen = new(0.102f, 0.165f, 0.102f);

        /// <summary>深蓝 #1e3a5f — 信任桶 0.00–0.40（真源 TRUST-09，L1 §2.1.2）。</summary>
        public static readonly Color DeepBlue = new(0.118f, 0.227f, 0.373f);

        // ─────────── 点缀色（≤3% 画面，§2.3） ───────────

        /// <summary>亮金 #d4af37 — 仅用于"确信"描边（真源 INTEL-07 ≥85%）、高光、技能就绪。</summary>
        public static readonly Color BrightGold = new(0.831f, 0.686f, 0.216f);

        // ─────────── 阵营色（§5.2） ───────────

        /// <summary>赵方单位底色（赭石，§5.2 轻骑色；与战旗同色系）。</summary>
        public static readonly Color ZhaoUnit = Ochre;

        /// <summary>秦方单位底色（朱红，§5.2 弩兵色；与战旗同色系）。</summary>
        public static readonly Color QinUnit = Vermilion;

        /// <summary>赵方建筑（赭石深版，让单位突出）。</summary>
        public static readonly Color ZhaoProp = new(0.400f, 0.320f, 0.200f);

        /// <summary>秦方建筑（朱红深版）。</summary>
        public static readonly Color QinProp = new(0.500f, 0.190f, 0.130f);

        // ─────────── 地形（§4.2 MVP） ───────────

        /// <summary>地形底色（土黄浅版，不与单位底色冲突）。</summary>
        public static readonly Color Ground = new(0.550f, 0.470f, 0.300f);

        /// <summary>减速通行地形底色（深墨绿，叠加半透明条纹纹理引用真源 COMBAT-16）。</summary>
        public static readonly Color DifficultTerrain = DeepMossGreen;

        /// <summary>不可通行地形（绝壁/深水/壁垒非缺口段）——墨黑加深灰调，与焦土区分（真源 COMBAT-05/18）。</summary>
        public static readonly Color ImpassableTerrain = new(0.180f, 0.165f, 0.150f);

        /// <summary>焦土（墨黑）。</summary>
        public static readonly Color Burned = InkBlack;

        // ─────────── 战场状态色 ───────────

        /// <summary>生命值高（亮金）。</summary>
        public static readonly Color Health = BrightGold;

        /// <summary>生命值低（朱红）。</summary>
        public static readonly Color HealthLow = Vermilion;

        /// <summary>士气（赭石）。</summary>
        public static readonly Color Morale = Ochre;

        /// <summary>士气崩溃（朱红）。</summary>
        public static readonly Color MoraleRouted = Vermilion;

        /// <summary>结阵光环（赭石半透明）。</summary>
        public static readonly Color Formation = new(0.650f, 0.550f, 0.400f, 0.55f);

        /// <summary>选中圈（亮金半透明）。</summary>
        public static readonly Color Selection = new(0.831f, 0.686f, 0.216f, 0.65f);

        /// <summary>火区（朱红半透明）。</summary>
        public static readonly Color Fire = new(0.659f, 0.255f, 0.169f, 0.45f);

        // ─────────── UI 色 ───────────

        /// <summary>面板背景（墨黑半透明）。</summary>
        public static readonly Color PanelBackground = new(0.100f, 0.090f, 0.070f, 0.85f);

        /// <summary>面板边框（赭石）。</summary>
        public static readonly Color PanelBorder = new(0.545f, 0.435f, 0.278f, 0.9f);

        /// <summary>主文字色（土黄，暖色主文字）。</summary>
        public static readonly Color TextPrimary = EarthYellow;

        /// <summary>次文字色（灰土黄）。</summary>
        public static readonly Color TextMuted = new(0.500f, 0.420f, 0.270f);

        // ─────────── 情报档位三色（真源 INTEL-07 + §2.2 仿古化） ───────────

        /// <summary>情报档位·确信（≥85%，亮金；真源 INTEL-07 ≥85% 绿色描金边）。</summary>
        public static readonly Color TierConfident = BrightGold;

        /// <summary>情报档位·可信（70–84%，石青；真源 INTEL-07 绿）。</summary>
        public static readonly Color TierTrusted = StoneBlue;

        /// <summary>情报档位·存疑（50–69%，赭石；真源 INTEL-07 黄）。</summary>
        public static readonly Color TierDoubtful = Ochre;

        /// <summary>情报档位·不可信（<50%，朱红；真源 INTEL-07 红）。</summary>
        public static readonly Color TierUntrusted = Vermilion;

        // ─────────── 技能槽色 ───────────

        /// <summary>技能就绪（亮金）。</summary>
        public static readonly Color SkillReady = BrightGold;

        /// <summary>技能冷却（灰土黄）。</summary>
        public static readonly Color SkillCooling = new(0.400f, 0.340f, 0.220f);

        /// <summary>技能不可用（朱红）。</summary>
        public static readonly Color SkillBlocked = Vermilion;

        // ─────────── 相机背景 ───────────

        /// <summary>主相机背景（墨黑，对应 SliceGame.MainCamera.clearFlags = SolidColor）。</summary>
        public static readonly Color CameraBackground = InkBlack;
    }

}