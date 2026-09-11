
namespace ChinaBettle.Foundation.Intel
{
    /// <summary>
    /// 情报层可信度全部可调参数（真源 INTEL-01~08）。
    /// 默认实例给出真源表初值；运行期由数据配置（ScriptableObject 等）注入，代码不得散落魔法数字。
    /// </summary>
    public sealed record CredibilityConfig(
        float ScoutVisualBase = 65f,
        float PrisonerBase = 35f,
        float CapturedDocumentBase = 80f,
        float SkillRevealBase = 75f,
        float PatternBaseMin = 40f,
        float PatternBaseMax = 75f,
        float ScoutCountBonusPerUnit = 2f,
        int ScoutCountCap = 10,
        float ObservationBonusPerMinute = 1f,
        int ObservationMinuteCap = 20,
        float AgeFloorRatio = 0.30f,
        float ExpiryMinutes = 15f,
        float ScoutVisualWeight = 1.0f,
        float CapturedDocumentWeight = 1.2f,
        float PrisonerWeight = 0.6f,
        float SkillRevealWeight = 1.0f,
        float PatternInferenceWeight = 0.9f,
        float DoubtfulThreshold = 50f,
        float TrustedThreshold = 70f,
        float ConfidentThreshold = 85f,
        float FlaggedFakeCredibility = 15f)
    {
        public static CredibilityConfig Default { get; } = new();
    }

}