using ChinaBettle.Foundation.Intel;

namespace ChinaBettle.Intel
{
    /// <summary>
    /// 权威端附加字段（情报规格 §8.2；NET-02）：真实载荷与欺骗元数据。
    /// 单机原型中存于"本地权威侧"；联网时仅存服务端，永不下发接收方。
    /// 识破后服务端才向接收方下发 status=flagged_fake 与 credibility_shown=15。
    /// </summary>
    public sealed class AuthoritativeIntel
    {
        public string IntelId { get; init; }

        public IntelPayload TrueContent { get; init; }

        public bool IsFabricated { get; set; }

        /// <summary>"player" / "ai"。</summary>
        public string? FabricatedBy { get; set; }

        public string? SkillId { get; set; }

        /// <summary>载荷篡改系数（DECP-02/03：0.3 / 1.5），来自技能配置，非硬编码。</summary>
        public float ContentMultiplier { get; set; } = 1f;

        public bool Detected { get; set; }
    }

}