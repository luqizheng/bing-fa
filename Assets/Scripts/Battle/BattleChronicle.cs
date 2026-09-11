using System.Collections.Generic;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Units;

namespace ChinaBettle.Battle
{
    /// <summary>编年史事件类别（GDD §5.4：历史事件以可回放"编年史"保存）。</summary>
    public enum ChronicleKind
    {
        /// <summary>情报生成/更新（含斥候目视、技能暴露）。</summary>
        Intel,

        /// <summary>施计（减灶/增灶/火攻）。</summary>
        Cast,

        /// <summary>识破（近距清点 / 异质信源交叉）。</summary>
        Detection,

        /// <summary>接战与伤亡。</summary>
        Combat,

        /// <summary>士气变化（含溃逃）。</summary>
        Morale,

        /// <summary>补给与饥饿。</summary>
        Supply,

        /// <summary>AI 决策（含门控与复核）。</summary>
        AiDecision,

        /// <summary>胜负结算。</summary>
        Outcome,
    }

    /// <summary>编年史条目。</summary>
    public sealed record ChronicleEntry(float AtSeconds, ChronicleKind Kind, string Text)
    {
        /// <summary>"mm:ss" 战役时刻（UI 展示）。</summary>
        public string TimeLabel => $"{(int)(AtSeconds / 60f):00}:{(int)(AtSeconds % 60f):00}";
    }

    /// <summary>
    /// 情报栏条目（SLICE-03④；UI-02/03）：接收方视角的一条情报。
    /// 只含接收方可见字段——真值/欺骗元数据在权威端（NET-02），UI 不得读取。
    /// </summary>
    public sealed class IntelFeedItem
    {
        public string IntelId { get; init; }

        public string Topic { get; init; }

        public IntelSourceType SourceType { get; init; }

        /// <summary>载荷（兵力估算；被欺骗时为篡改后数值）。</summary>
        public float Value { get; init; }

        /// <summary>当前可信度 0–100（年龄衰减后，INTEL-04）。</summary>
        public float Credibility { get; init; }

        public CredibilityTier Tier { get; init; }

        public float CreatedAtSeconds { get; init; }

        public IntelStatus Status { get; init; }

        /// <summary>UI-03：被识破的伪造条目显示"？"印章。</summary>
        public bool ShowFakeStamp => Status == IntelStatus.FlaggedFake;
    }

    /// <summary>双方战果统计（结算面板用）。</summary>
    public sealed class BattleStats
    {
        public int ZhaoKills { get; set; }

        public int QinKills { get; set; }

        public int ZhaoLost { get; set; }

        public int QinLost { get; set; }

        public int PlayerDetections { get; set; }

        public int AiDetections { get; set; }

        public int PlayerCasts { get; set; }

        public int AiCasts { get; set; }
    }

    /// <summary>战役结果。</summary>
    public enum BattleOutcome
    {
        Undecided,

        /// <summary>赵（玩家）胜。</summary>
        ZhaoVictory,

        /// <summary>秦（AI）胜。</summary>
        QinVictory,

        Draw,
    }

    /// <summary>结果详情。</summary>
    public sealed record BattleOutcomeInfo(BattleOutcome Outcome, string Reason);

}