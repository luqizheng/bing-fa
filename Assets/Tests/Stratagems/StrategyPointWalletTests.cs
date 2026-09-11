using ChinaBettle.Stratagems;
using NUnit.Framework;

namespace ChinaBettle.Tests.Stratagems
{
    /// <summary>谋略点经济回归（真源 SKILL-11）。</summary>
    public sealed class StrategyPointWalletTests
    {
        [Test]
        public void Prototype_StartsWith100()
        {
            Assert.That(new StrategyPointWallet().Points, Is.EqualTo(100));
        }

        [Test]
        public void DetectReward_Is15_CappedAt100()
        {
            var wallet = new StrategyPointWallet(initialPoints: 95);
            Assert.That(wallet.AwardDeceptionDetected(), Is.EqualTo(5), "溢出不累积");
            Assert.That(wallet.Points, Is.EqualTo(100));
        }

        [Test]
        public void Spend_ReduceStoveCost30()
        {
            var wallet = new StrategyPointWallet();
            Assert.That(wallet.TrySpend(30), Is.True);
            Assert.That(wallet.Points, Is.EqualTo(70));
            Assert.That(wallet.TrySpend(100), Is.False, "点数不足不得释放（切片：阵营计30+火攻40=70，余30）");
        }

        [Test]
        public void PerDecadeRegen_Adds20_DoesNotRollOverAndCapsAt100()
        {
            var wallet = new StrategyPointWallet(initialPoints: 90);
            Assert.That(wallet.GrantPerDecade(), Is.EqualTo(10));
            Assert.That(wallet.Points, Is.EqualTo(100));
            Assert.That(wallet.GrantPerDecade(), Is.EqualTo(0), "跨旬不累积：满点后作废");
        }
    }

}