
namespace ChinaBettle.Game
{
    /// <summary>
    /// 工程总说明（占位类型，仅用于锚定 Game 程序集）。
    /// MVP 范围（真源 SLICE-01/02、SKILL-10）：赵 vs 秦、单图滏口陉–野王–高都、
    /// 减灶/增灶/火攻三技能、廉颇+赵括两幕僚、单战役剧本。
    /// 七国全阵营/十二计其余九计/多人对战/战役编辑器均为第二阶段以后，未经明确要求不得提前实现。
    /// 注意：必须保持 <c>public</c>——Editor 程序集（ChinaBettle.Editor）在启动横幅里读它，
    /// 改成 internal 会让 Editor 程序集编译失败（CS0122）。
    /// </summary>
    public static class GameScope
    {
        public const string ProjectDisplayName = "战国·兵者诡道";

        public const string ProjectEnglishName = "Warring States: The Art of Deception";
    }

}