using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Foundation
{
    /// <summary>克制环与伤害公式回归（真源 COMBAT-01/02/08/09/12）。</summary>
    public sealed class CombatFormulaTests
    {
        [Test]
        public void CounterRing_FollowsSpearCavalryCrossbowHeavyCycle()
        {
            var ring = new CounterRing();
            Assert.That(ring.Coefficient(BaseUnitClass.Spear, BaseUnitClass.LightCavalry), Is.EqualTo(1.35f));
            Assert.That(ring.Coefficient(BaseUnitClass.LightCavalry, BaseUnitClass.Crossbow), Is.EqualTo(1.35f));
            Assert.That(ring.Coefficient(BaseUnitClass.Crossbow, BaseUnitClass.HeavyInfantry), Is.EqualTo(1.35f));
            Assert.That(ring.Coefficient(BaseUnitClass.HeavyInfantry, BaseUnitClass.Spear), Is.EqualTo(1.35f));

            // 反方向=被克 ×0.75
            Assert.That(ring.Coefficient(BaseUnitClass.LightCavalry, BaseUnitClass.Spear), Is.EqualTo(0.75f));
            // 同类 ×1.00
            Assert.That(ring.Coefficient(BaseUnitClass.Spear, BaseUnitClass.Spear), Is.EqualTo(1.00f));
        }

        [Test]
        public void Damage_HasFloorOfOne()
        {
            // COMBAT-08：max(1, 攻击×系数×士气×地形 − 防御)
            int dmg = DamageFormula.Resolve(
                panelAttack: 10f, defense: 500f,
                counterCoefficient: 0.75f, morale: MoraleState.Normal, terrainCoefficient: 1f);
            Assert.That(dmg, Is.EqualTo(1));
        }

        [Test]
        public void Damage_RoutedUnit_DealsZero()
        {
            int dmg = DamageFormula.Resolve(100f, 0f, 1.35f, MoraleState.Routed, 1f);
            Assert.That(dmg, Is.EqualTo(0));
        }

        [Test]
        public void Morale_Boundaries()
        {
            Assert.That(Morale.Classify(100), Is.EqualTo(MoraleState.High));
            Assert.That(Morale.Classify(79), Is.EqualTo(MoraleState.Normal));
            Assert.That(Morale.Classify(20), Is.EqualTo(MoraleState.Shaken));
            Assert.That(Morale.Classify(19), Is.EqualTo(MoraleState.Routed));
        }
    }
}