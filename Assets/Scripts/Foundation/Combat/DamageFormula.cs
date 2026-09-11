using System;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Foundation.Combat
{
    /// <summary>
    /// 接战伤害公式（真源 COMBAT-08）：
    /// 实际伤害 = max(1, 攻击力 × 克制系数 × 士气系数 × 地形系数 − 防御力)，保底 1。
    /// 注意口径（COMBAT-12）：高昂 +10%/动摇 −20% 的攻击修正在【面板攻击】结算，
    /// 士气系数只取 高昂/正常 ×1.0、动摇 ×0.8（崩溃不作战），不重复乘。
    /// </summary>
    public static class DamageFormula
    {
        public static int Resolve(
            float panelAttack,
            float defense,
            float counterCoefficient,
            MoraleState morale,
            float terrainCoefficient,
            CombatConfig? config = null)
        {
            var c = config ?? CombatConfig.Default;
            if (morale == MoraleState.Routed)
            {
                // 崩溃单位不作战（COMBAT-09：自动向最近己方城池溃逃、不可控）。
                return 0;
            }

            float moraleCoefficient = morale == MoraleState.Shaken ? c.MoraleShakenCoefficient : 1.0f;
            float raw = panelAttack * counterCoefficient * moraleCoefficient * terrainCoefficient - defense;
            return Math.Max(c.MinDamage, (int)MathF.Floor(raw));
        }
    }

    /// <summary>士气四档（真源 COMBAT-09）。</summary>
    public enum MoraleState
    {
        /// <summary>80–100：攻 +10%、移速 +5%（面板修正）。</summary>
        High,

        /// <summary>50–79：无加成。</summary>
        Normal,

        /// <summary>20–49：攻 −20%（面板）、移速 −10%、士气系数 ×0.8。</summary>
        Shaken,

        /// <summary>&lt;20：崩溃，溃逃不可控、不作战。</summary>
        Routed,
    }

    /// <summary>士气档位归类（COMBAT-09）。</summary>
    public static class Morale
    {
        public static MoraleState Classify(int morale0To100) => morale0To100 switch
        {
            >= 80 => MoraleState.High,
            >= 50 => MoraleState.Normal,
            >= 20 => MoraleState.Shaken,
            _ => MoraleState.Routed,
        };
    }

}