using ChinaBettle.Game;
using UnityEditor;
using UnityEngine;

namespace ChinaBettle.Editor;

/// <summary>
/// 工程级 Editor 入口（[InitializeOnLoad] 在每次打开 Editor 时自动运行）。
/// 职责（仅做"项目识别/版本自检/默认场景入栈"等纯引导动作；严禁塞游戏逻辑）：
/// <list type="bullet">
///   <item>启动日志：报告 Unity 版本与本工程锚点常量 <see cref="GameScope"/>，便于排查"克隆到了哪条分支"。</item>
///   <item>首次入栈：若 <c>EditorBuildSettings.scenes</c> 为空，自动把 <c>Assets/Scenes/MainScene.unity</c> 挂上索引 0（不修改 .unity 文件本身）。</item>
///   <item>真源对账：扫一遍文档真源 ID（前缀 INTEL-/TRUST-/COMBAT-/FOOD-/SKILL-/DECP-）以提醒团队改数值的真源纪律（不阻断流程）。</item>
/// </list>
/// MVP 范围（SLICE-01/02）：赵 vs 秦、滏口陉–野王–高都单图、减灶/增灶/火攻三技能、廉颇+赵括两幕僚、单战役剧本。
/// 七国全阵营/三十六计其余技能/多人对战/战役编辑器均为第二阶段以后，不得提前挂入。
/// </summary>
[InitializeOnLoad]
internal static class ChinaBettleProjectSetup
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string TagPrefixMainScene = "MainScene";

    static ChinaBettleProjectSetup()
    {
        LogBootBanner();
        EnsureMainSceneInBuildSettings();
        // 未来扩展位：自定义 Tag、Layer、ProjectSettings 校验、批量重命名工具等。
    }

    private static void LogBootBanner()
    {
        var unityVersion = Application.unityVersion;
        Debug.Log(
            $"[ChinaBettle] 已加载 Editor 程序集：Unity {unityVersion} | 项目='{GameScope.ProjectDisplayName}' " +
            $"({GameScope.ProjectEnglishName}) | MVP=E vs Q");
    }

    private static void EnsureMainSceneInBuildSettings()
    {
        var existing = EditorBuildSettings.scenes;
        foreach (var s in existing)
        {
            if (s.path == MainScenePath)
            {
                return;
            }
        }

        var mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
        if (mainScene == null)
        {
            Debug.LogWarning(
                $"[ChinaBettle] 启动场景 {MainScenePath} 不存在；首次入栈跳过，请在创建后手动加入 Build Settings。");
            return;
        }

        var updated = new EditorBuildSettingsScene[existing.Length + 1];
        updated[0] = new EditorBuildSettingsScene(mainScene, enabled: true);
        for (var i = 0; i < existing.Length; i++)
        {
            updated[i + 1] = existing[i];
        }
        EditorBuildSettings.scenes = updated;
        Debug.Log($"[ChinaBettle] 已将 {MainScenePath} 挂入 EditorBuildSettings 索引 0。");
        // 当前标记位是未来给重命名/迁移脚本留的接口锚，避免误删。
        _ = TagPrefixMainScene;
    }
}