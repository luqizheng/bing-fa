
namespace ChinaBettle.AI
{
    /// <summary>
    /// 轻量玩家画像（真源 AI-03）——适应模块原型：
    /// 统计近 N 场侧翼袭击率、欺骗技能频率、常用进攻时段，写入行为树权重；
    /// 不使用 ML-Agents 进决策主链路（AI-04：ML 推迟三期离线实验）。
    /// </summary>
    public sealed record PlayerProfile(
        int RecentMatchWindow,
        float FlankAttackRate,
        float DeceptionSkillFrequency,
        float PreferredAttackHourNormalized)
    {
        /// <summary>侧翼护卫权重修正示例（GDD §3.1：侧翼被袭率 &gt;60% → 粮道护卫权重 +0.3）。</summary>
        public float SupplyEscortWeightDelta(float flankRateThreshold = 0.60f, float delta = 0.30f)
            => FlankAttackRate > flankRateThreshold ? delta : 0f;
    }

}