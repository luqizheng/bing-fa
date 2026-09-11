using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>
    /// 切片启动入口：**不依赖场景内容**——按 Play 即在运行期构建战役场景、相机与 HUD。
    /// 这样 MainScene 保持最小骨架（AGENTS.md 约定：场景内不预放 GameObject/Prefab），
    /// 同时避免手工编辑 Unity 场景 YAML 带来的资产引用风险；
    /// 后续若要转成"场景内正式接线"，把本类换成挂在场景里的一个 MonoBehaviour 即可，规则层无需改动。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<SliceGame>() is not null)
            {
                return;
            }

            var root = new GameObject("ChinaBettle.SliceGame");
            root.AddComponent<SliceGame>();
            Debug.Log("[ChinaBettle] 垂直切片启动：赵 vs 秦 · 长平之战主战场（SLICE-01）。" +
                      "F1 减灶 / F2 增灶 / F3 火攻；左键选择、右键移动或攻击；空格暂停。");
        }
    }

}