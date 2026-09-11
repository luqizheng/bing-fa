using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Foundation.Combat
{
    /// <summary>
    /// 结阵姿态规则（真源 COMBAT-17）：白板枪兵/矛阵新增"结阵"姿态（默认开、可切换）——
    /// ①移速 −20%；②免疫骑兵冲锋击退；③拒马反噬：骑兵单位（轻骑/赵骑）对结阵枪兵
    /// 发起骑射/远程攻击时，骑兵自身承受 6 点/发反噬伤害。
    /// 落地依据：纯近身反制够不着赵骑 150m 骑射，故以"骑射反噬"兑现"枪兵克轻骑"。
    /// 纯逻辑、无 Unity 依赖；姿态开合与击退结算的执行归 Battle 层单位系统。
    /// </summary>
    public static class FormationRules
    {
        /// <summary>结阵移速系数（COMBAT-17①）：结阵 ×(1−减速惩罚)，未结阵 ×1.0。</summary>
        public static float MoveMultiplier(bool formed, CombatConfig? config = null)
        {
            var c = config ?? CombatConfig.Default;
            return formed ? 1f - c.SpearFormationMovePenalty : NeutralMultiplier(c);
        }

        /// <summary>免疫骑兵冲锋击退（COMBAT-17②）：仅结阵时免疫。</summary>
        public static bool IsImmuneToChargeKnockback(bool formed) => formed;

        /// <summary>
        /// 拒马反噬（COMBAT-17③）：单发反噬伤害——骑兵攻击者 + 结阵防御者 → 6 点/发，否则 0。
        /// 攻速结算归 Battle 层（赵骑 0.5 攻速 ×6 点 → 反噬 DPS 3.0，白嫖结阵枪兵约 33s 自毙）。
        /// </summary>
        public static int CavalryReflectDamage(bool attackerIsCavalry, bool defenderFormed, CombatConfig? config = null)
        {
            var c = config ?? CombatConfig.Default;
            return attackerIsCavalry && defenderFormed ? c.CavalryReflectDamagePerHit : 0;
        }

        private static float NeutralMultiplier(CombatConfig c) => c.NeutralMultiplier;
    }

}