using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Foundation
{
    /// <summary>
    /// 结阵反骑与火区结算回归（真源 COMBAT-17、SKILL-03）。
    /// 锁定算例：结阵移速 ×0.8、免疫冲锋击退、骑兵反噬 6 点/发；火区 60 秒、灼烧 8 点/2 秒。
    /// </summary>
    public sealed class FormationAndFireZoneTests
    {
        [Test]
        public void FormedSpear_MovePenalty_Immunity_AndReflect()
        {
            // COMBAT-17①：结阵移速 −20%。
            Assert.That(FormationRules.MoveMultiplier(formed: true), Is.EqualTo(0.8f).Within(1e-5));
            Assert.That(FormationRules.MoveMultiplier(formed: false), Is.EqualTo(1.0f).Within(1e-5));

            // COMBAT-17②：仅结阵免疫冲锋击退。
            Assert.That(FormationRules.IsImmuneToChargeKnockback(formed: true), Is.True);
            Assert.That(FormationRules.IsImmuneToChargeKnockback(formed: false), Is.False);

            // COMBAT-17③：骑兵 vs 结阵枪兵 → 6 点/发反噬；其余组合 0。
            Assert.That(FormationRules.CavalryReflectDamage(attackerIsCavalry: true, defenderFormed: true), Is.EqualTo(6));
            Assert.That(FormationRules.CavalryReflectDamage(attackerIsCavalry: true, defenderFormed: false), Is.Zero,
                "未结阵枪兵无拒马，骑兵可白嫖");
            Assert.That(FormationRules.CavalryReflectDamage(attackerIsCavalry: false, defenderFormed: true), Is.Zero,
                "步兵远程不触发反噬");
        }

        [Test]
        public void ReflectDamage_ComesFromConfig()
        {
            var config = new CombatConfig(CavalryReflectDamagePerHit: 9);
            Assert.That(FormationRules.CavalryReflectDamage(true, true, config), Is.EqualTo(9));
        }

        [Test]
        public void FireZone_BurnsEvery2Seconds_At8PerTick()
        {
            var zone = new FireZone(createdAtSeconds: 100f);

            // 起火时刻尚未到第一个 tick（+2s）。
            Assert.That(zone.Settle(101f), Is.Zero);

            // (101, 106] 内发生 102/104/106 三个 tick → 24 点。
            Assert.That(zone.Settle(106f), Is.EqualTo(24));

            // (106, 110] 内 108/110 两个 tick → 16 点。
            Assert.That(zone.Settle(110f), Is.EqualTo(16));

            // 燃烧边界：159.9 仍在烧，160.0（=100+60）已熄。
            Assert.That(zone.IsActiveAt(159.9f), Is.True);
            Assert.That(zone.IsActiveAt(160f), Is.False);
        }

        [Test]
        public void FireZone_StopsDamageAfterExpiry_AndIgnoresRewoundTime()
        {
            var zone = new FireZone(createdAtSeconds: 0f);

            // 烧满全程：60 秒 / 2 秒 = 30 tick × 8 = 240。
            Assert.That(zone.Settle(60f), Is.EqualTo(240));

            // 熄灭后再结算不产生新伤害；时间倒退（乱序帧）也不产生伤害。
            Assert.That(zone.Settle(1000f), Is.Zero);
            Assert.That(zone.Settle(10f), Is.Zero);
            Assert.That(zone.IsActiveAt(1000f), Is.False);
        }

        [Test]
        public void FireZone_ConfigHonored()
        {
            var config = new CombatConfig(
                FireZoneDurationSeconds: 30f,
                FireZoneBurnIntervalSeconds: 5f,
                FireZoneBurnDamagePerTick: 3);
            var zone = new FireZone(0f, config);

            Assert.That(zone.EndSeconds, Is.EqualTo(30f));
            Assert.That(zone.Settle(30f), Is.EqualTo(6 * 3), "30 秒 / 5 秒 = 6 tick × 3 点");
        }
    }
}