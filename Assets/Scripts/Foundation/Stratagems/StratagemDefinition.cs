using ChinaBettle.Foundation.Time;

namespace ChinaBettle.Foundation.Stratagems
{
    /// <summary>
    /// 诡道技能的【数据驱动定义】（GDD §2.3：谋略点消耗 + 双层冷却，部分全局仅一次）。
    /// 新技能走配置（ScriptableObject/JSON），不得硬编码效果；篡改系数（×0.3/×1.5 等）是本定义数据（DECP-01）。
    /// 原型切片数据：SKILL-01~03 / DECP-02/03。
    /// </summary>
    public sealed record StratagemDefinition(
        StratagemId Id,
        StratagemCategory Category,
        int StrategyPointCost,
        float BattleCooldownSeconds,
        int StrategicCooldownDecades,
        bool OncePerBattle,
        float CastWindowSeconds,
        float TraceDurationSeconds,
        float TroopEstimateMultiplier,
        bool FabricatesIntel,
        string DisplayName)
    {
        /// <summary>该技能的 CD 挂载层（合纵连横挂战略层，其余战场层；GDD §2.3 冷却归属规则）。</summary>
        public TimeLayer CooldownLayer => StrategicCooldownDecades > 0 ? TimeLayer.Strategic : TimeLayer.Battle;

        public bool HasBattleCooldown => BattleCooldownSeconds > 0f;

        /// <summary>切片三技能真源初值（SKILL-01/02/03、DECP-02/03）。</summary>
        public static readonly StratagemDefinition ReduceStove = new(
            StratagemId.ReduceStove, StratagemCategory.Deception,
            StrategyPointCost: 30, BattleCooldownSeconds: 90f, StrategicCooldownDecades: 0,
            OncePerBattle: false, CastWindowSeconds: 90f, TraceDurationSeconds: 600f,
            TroopEstimateMultiplier: 0.3f, FabricatesIntel: true, DisplayName: "减灶示弱");

        public static readonly StratagemDefinition AddStove = new(
            StratagemId.AddStove, StratagemCategory.Deception,
            StrategyPointCost: 30, BattleCooldownSeconds: 90f, StrategicCooldownDecades: 0,
            OncePerBattle: false, CastWindowSeconds: 90f, TraceDurationSeconds: 600f,
            TroopEstimateMultiplier: 1.5f, FabricatesIntel: true, DisplayName: "增灶示强");

        public static readonly StratagemDefinition FireAttack = new(
            StratagemId.FireAttack, StratagemCategory.Raid,
            StrategyPointCost: 40, BattleCooldownSeconds: 120f, StrategicCooldownDecades: 0,
            OncePerBattle: false, CastWindowSeconds: 0f, TraceDurationSeconds: 0f,
            TroopEstimateMultiplier: 1f, FabricatesIntel: false, DisplayName: "火攻粮道");
    }

}