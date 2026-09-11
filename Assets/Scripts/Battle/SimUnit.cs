using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using ChinaBettle.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 战场单位运行时状态（1 队 = 100 人，COMBAT-13）。
    /// 携带士气状态机（COMBAT-09/10）、结阵姿态（COMBAT-17）、携粮与饥饿度（FOOD-01/04）、军功（COMBAT-15）。
    /// </summary>
    public sealed class SimUnit
    {
        /// <summary>COMBAT-13：1 个单位 = 100 人/队（与 FOOD-02 口粮口径一致）。</summary>
        public const int MenPerUnit = 100;

        /// <summary>FOOD-01：每队出征携粮 3 日粮 = 18 战场分钟 = 18 个"单位粮草"（FOOD-02：每 60 秒耗 1 单位）。</summary>
        public const float MaxRationsUnits = 18f;

        public SimUnit(string id, Faction faction, UnitDefinition definition, MapPoint position, BattleRules rules)
        {
            Id = id;
            Faction = faction;
            Definition = definition;
            Position = position;
            Health = definition.MaxHealth;
            Morale = new MoraleStateMachine(startMorale: 50, rules.Combat);
            Formed = definition.SpearFormationCapable; // 结阵默认开（COMBAT-17）
            RationsUnits = MaxRationsUnits;
        }

        public string Id { get; }

        /// <summary>展示名（编年史/UI 用，如"赵骑1(主将)"）。</summary>
        public string DisplayLabel { get; set; } = string.Empty;

        public Faction Faction { get; }

        public UnitDefinition Definition { get; }

        public MapPoint Position { get; set; }

        public float Health { get; set; }

        public MoraleStateMachine Morale { get; }

        /// <summary>结阵姿态（仅枪兵可结阵；默认开，可切换）。</summary>
        public bool Formed { get; set; }

        /// <summary>玩家/AI 下达的移动目标；为 null 表示原地待命。</summary>
        public MapPoint? MoveGoal { get; set; }

        /// <summary>当前交战目标单位 Id。</summary>
        public string? AttackTargetId { get; set; }

        /// <summary>攻击冷却计时（秒）。</summary>
        public float AttackCooldownSeconds { get; set; }

        public float RationsUnits { get; set; }

        /// <summary>饥饿度 0–1（FOOD-04）。</summary>
        public float Starvation { get; private set; }

        /// <summary>军功爵位击杀数（COMBAT-15，秦锐士）。</summary>
        public int Kills { get; private set; }

        /// <summary>累计目击时长（情报规格 INTEL-03，分钟）。</summary>
        public float ObservationMinutes { get; set; }

        /// <summary>已离场（阵亡 / 溃逃回城 / 饿散）。</summary>
        public bool Removed { get; set; }

        public bool IsCommander => Definition.IsCommander;

        public bool IsScout => Definition.IsScout;

        public bool IsCavalry => Definition.Class == BaseUnitClass.LightCavalry;

        public bool Alive => Health > 0f && !Removed;

        public MoraleState MoraleState => Morale.State;

        /// <summary>溃逃中（<20，不作战、自动向己方城池溃逃，COMBAT-09）。</summary>
        public bool IsRouted => MoraleState == MoraleState.Routed && Alive;

        public float HealthRatio => System.Math.Clamp(Health / Definition.MaxHealth, 0f, 1f);

        public void RegisterKill() => Kills++;

        /// <summary>FOOD-04：粮尽后每 60 秒 +10%；到 100% 视为饿散（由仿真层移出）。</summary>
        /// <summary>FOOD-06：补给恢复后饥饿度回落（不清零）。</summary>
        public void RecoverStarvation(float amount) =>
            Starvation = System.Math.Clamp(Starvation - amount, 0f, 1f);

        public void ApplyStarvationTick(float gain) =>
            Starvation = System.Math.Clamp(Starvation + gain, 0f, 1f);

        public void Resupply() => RationsUnits = MaxRationsUnits;

        /// <summary>FOOD-02：每 60 秒消耗 1 单位粮草；返回是否本 tick 粮尽。</summary>
        public bool ConsumeRationTick(float unitsPerTick)
        {
            RationsUnits = System.Math.Max(0f, RationsUnits - unitsPerTick);
            return RationsUnits <= 0f;
        }

        /// <summary>
        /// 面板攻击（COMBAT-12 口径）：基础攻击 × 士气面板修正（高昂 +10% / 动摇 −20%）
        /// × 饥饿攻击惩罚（FOOD-04，>50% 时 −20%）× 秦锐士军功加成（COMBAT-15，+1%/杀、上限 +20%）。
        /// **不含克制系数与地形**——那两项在伤害公式与克制环里结算，避免重复乘算。
        /// </summary>
        public float PanelAttack(BattleRules rules)
        {
            float attack = Definition.Attack;

            attack *= MoraleState switch
            {
                MoraleState.High => 1f + rules.Combat.MoraleHighAttackModifier,
                MoraleState.Shaken => 1f + rules.Combat.MoraleShakenAttackModifier,
                _ => 1f,
            };

            if (Starvation > rules.Supply.StarvationAttackPenaltyThreshold)
            {
                attack *= 1f + rules.Supply.StarvationAttackPenalty;
            }

            if (Definition.QinRankBonus)
            {
                attack *= 1f + System.MathF.Min(Kills * rules.Combat.QinRankAttackBonusPerKill, rules.Combat.QinRankAttackBonusCap);
            }

            return attack;
        }

        /// <summary>
        /// 当前移速（米/秒）：基础 × 地形修正（COMBAT-16，赵骑忽略） × 结阵惩罚（COMBAT-17）
        /// × 士气移速修正（COMBAT-09） × 饥饿移速惩罚（FOOD-04）。
        /// </summary>
        public float CurrentMoveSpeed(BattleRules rules, TerrainClass terrain)
        {
            float speed = Definition.MoveSpeed;

            if (terrain == TerrainClass.Difficult && !Definition.IgnoresDifficultTerrain)
            {
                speed *= rules.Combat.DifficultTerrainSpeedMultiplier;
            }

            if (Formed)
            {
                speed *= 1f - rules.Combat.SpearFormationMovePenalty;
            }

            speed *= MoraleState switch
            {
                MoraleState.High => 1f + rules.Combat.MoraleHighMoveModifier,
                MoraleState.Shaken => 1f + rules.Combat.MoraleShakenMoveModifier,
                _ => 1f,
            };

            if (Starvation > rules.Supply.StarvationAttackPenaltyThreshold)
            {
                speed *= 1f + rules.Supply.StarvationMovePenalty;
            }

            return System.MathF.Max(0f, speed);
        }
    }

}