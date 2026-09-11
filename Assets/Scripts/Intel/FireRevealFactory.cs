using ChinaBettle.Foundation.Intel;

namespace ChinaBettle.Intel
{
    /// <summary>
    /// 火攻浓烟情报工厂（真源 SKILL-03、SLICE-03⑤）：
    /// 起火瞬间向【所有对起火点有视野的一方】各生成一条 skill_reveal 情报——
    /// 基值 75%、位置=起火点、内容"敌方在此使用火攻"、不可伪造、不可拦截、无需斥候采样。
    /// 视野过滤由调用方（未来感知过滤，NET-01）决定，本工厂只产出记录。
    /// </summary>
    public static class FireRevealFactory
    {
        private static int sequence;

        public static IntelRecord Create(string topic, float firePointX, float firePointZ, float nowSeconds, string factionTag)
        {
            var config = CredibilityConfig.Default;
            var record = new IntelRecord
            {
                IntelId = $"INTEL_FIRE_{factionTag}_{System.Threading.Interlocked.Increment(ref sequence):D4}",
                Topic = topic,
                SourceType = IntelSourceType.SkillReveal,
                BaseCredibility = config.SkillRevealBase,
                RawContent = new IntelPayload(IntelPayloadType.FireReveal, 1f, "event"),
                ScoutCount = 0,
                ObservationMinutes = 0f,
                CreatedAtSeconds = nowSeconds,
            };

            // 技能暴露无目击修正；fresh = 基值（75）。
            float fresh = CredibilityCalculator.Fresh(config, IntelSourceType.SkillReveal, 0, 0f);
            record.Initialize(fresh);
            return record;
        }
    }

}