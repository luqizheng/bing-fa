using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 单位模板（真源 COMBAT-14 / 《基础兵种数值表 v0.2》§1–§2）。
    /// 数值为**整队属性**，1 队 = 100 人（COMBAT-13，与 FOOD-02 口粮口径一致）。
    /// 移速单位米/秒、射程米、攻速以"攻击间隔秒"表达（兵种表给的是次/秒，间隔 = 1/次）。
    /// </summary>
    public sealed record UnitDefinition(
        string Id,
        string DisplayName,
        BaseUnitClass Class,
        float MaxHealth,
        float Attack,
        float Defense,
        float MoveSpeed,
        float AttackRange,
        float AttackIntervalSeconds,
        float RangedAttack = 0f,
        float RangedRange = 0f,
        float RangedIntervalSeconds = 0f,
        bool IgnoresDifficultTerrain = false,
        bool SpearFormationCapable = false,
        bool QinRankBonus = false,
        bool IsScout = false,
        bool IsCommander = false)
    {
        public bool HasRangedAttack => RangedRange > 0f;
    }

    /// <summary>
    /// 切片单位目录（SLICE-01/02：赵 vs 秦；兵种表 v0.2 六模板 + 斥候占位）。
    /// </summary>
    public static class SliceUnitCatalog
    {
        /// <summary>枪兵/矛阵（兵种表 §1）：120/12/8，3.5 m/s，近战 12m，1.0 次/秒。可结阵（COMBAT-17）。</summary>
        public static readonly UnitDefinition Spear = new(
            "spear", "枪兵/矛阵", BaseUnitClass.Spear,
            MaxHealth: 120f, Attack: 12f, Defense: 8f, MoveSpeed: 3.5f,
            AttackRange: 12f, AttackIntervalSeconds: 1.0f, SpearFormationCapable: true);

        /// <summary>轻骑（兵种表 §1）：100/14/5，7.0 m/s。</summary>
        public static readonly UnitDefinition LightCavalry = new(
            "light_cav", "轻骑", BaseUnitClass.LightCavalry,
            MaxHealth: 100f, Attack: 14f, Defense: 5f, MoveSpeed: 7.0f,
            AttackRange: 12f, AttackIntervalSeconds: 1.0f);

        /// <summary>弩兵/弓兵（兵种表 §1）：70/16/3，射程 180m，0.4 次/秒 = 2.5s 装填。</summary>
        public static readonly UnitDefinition Crossbow = new(
            "crossbow", "弩兵/弓兵", BaseUnitClass.Crossbow,
            MaxHealth: 70f, Attack: 16f, Defense: 3f, MoveSpeed: 3.5f,
            AttackRange: 180f, AttackIntervalSeconds: 2.5f);

        /// <summary>重步（兵种表 §1）：160/10/12，2.8 m/s，0.8 次/秒 = 1.25s。</summary>
        public static readonly UnitDefinition HeavyInfantry = new(
            "heavy", "重步", BaseUnitClass.HeavyInfantry,
            MaxHealth: 160f, Attack: 10f, Defense: 12f, MoveSpeed: 2.8f,
            AttackRange: 12f, AttackIntervalSeconds: 1.25f);

        /// <summary>秦锐士（兵种表 §2，重步变体，总属性约 +24%）：200/12/15；军功爵位每击杀 +1% 攻击、上限 +20%（COMBAT-15）。</summary>
        public static readonly UnitDefinition QinRuiShi = new(
            "qin_ruishi", "秦锐士", BaseUnitClass.HeavyInfantry,
            MaxHealth: 200f, Attack: 12f, Defense: 15f, MoveSpeed: 2.8f,
            AttackRange: 12f, AttackIntervalSeconds: 1.25f, QinRankBonus: true, IsCommander: true);

        /// <summary>赵胡服骑射（兵种表 §2，轻骑变体）：近战 14/12m/1.0，骑射 12/150m/0.5（间隔 2s）；忽略减速地形（COMBAT-06/16）。</summary>
        public static readonly UnitDefinition ZhaoHuFu = new(
            "zhao_hufu", "赵胡服骑射", BaseUnitClass.LightCavalry,
            MaxHealth: 100f, Attack: 14f, Defense: 5f, MoveSpeed: 7.5f,
            AttackRange: 12f, AttackIntervalSeconds: 1.0f,
            RangedAttack: 12f, RangedRange: 150f, RangedIntervalSeconds: 2.0f,
            IgnoresDifficultTerrain: true, IsCommander: true);

        /// <summary>
        /// 斥候（兵种表 §5"斥候单位属性"**待 v0.3 定义**）：切片占位——低战力高机动、侦查用。
        /// 数值非真源，仅支撑 SLICE-03①"斥候目视生成情报"的验收路径；定案后回写兵种表与真源表。
        /// </summary>
        public static readonly UnitDefinition Scout = new(
            "scout", "斥候", BaseUnitClass.LightCavalry,
            MaxHealth: 30f, Attack: 2f, Defense: 2f, MoveSpeed: 6.5f,
            AttackRange: 12f, AttackIntervalSeconds: 0.5f, IsScout: true);
    }

}