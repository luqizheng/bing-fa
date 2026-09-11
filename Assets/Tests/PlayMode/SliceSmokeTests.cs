using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using ChinaBettle.Battle;
using ChinaBettle.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChinaBettle.Tests.PlayMode
{
    /// <summary>
    /// 垂直切片冒烟测试：回答"按下 Play 到底能不能跑"。
    ///
    /// 覆盖链路：<see cref="GameBootstrap"/>（RuntimeInitializeOnLoadMethod / AfterSceneLoad 自举）
    /// → <see cref="SliceGame"/>.Awake（建相机、光照、仿真、表现、HUD）
    /// → 每帧 Update（sim.Tick → view.Sync → hud.Refresh）。
    ///
    /// 任何 <c>Debug.LogError</c> 或未捕获异常都会被 Unity Test Framework 自动判为失败——
    /// 这正是需要的：Game 层的 8 个文件在本次之前从未被执行过。
    /// </summary>
    public sealed class SliceSmokeTests
    {
        /// <summary>
        /// 初始编制**取自地图定义**（不硬编码）——地图改部署（如按 MAP-15 补斥候）不应改本用例。
        /// 长平图当前为 19 队。
        /// </summary>
        private static int DeployedUnitCount => SliceMaps.ChangpingV1.Deployments.Count;

        /// <summary>观察窗口（真实秒）。按真实时间而非帧数，避免 batchmode 帧率波动造成假阴性。</summary>
        private const float ObserveSeconds = 3f;

        [UnityTest]
        public IEnumerator Slice_Boots_And_AdvancesBattleClock()
        {
            // GameBootstrap 挂在 AfterSceneLoad，进 Play 后首帧完成自举。
            yield return null;

            var game = UnityEngine.Object.FindFirstObjectByType<SliceGame>();
            Assert.IsNotNull(
                game,
                "GameBootstrap 未自举出 SliceGame —— RuntimeInitializeOnLoadMethod 未生效，按 Play 会是一片空场景。");

            var world = game.transform.Find("SliceWorld");
            Assert.IsNotNull(world, "SliceGame.Awake 未构建 SliceWorld。");

            int unitViews = 0;
            foreach (Transform child in world)
            {
                if (child.name.StartsWith("Unit_"))
                {
                    unitViews++;
                }
            }

            Assert.AreEqual(
                DeployedUnitCount,
                unitViews,
                "战场单位视图数量与 DeployInitialForces 不符（表现层未完整构建）。");

            Assert.IsNotNull(
                UnityEngine.Object.FindFirstObjectByType<Canvas>(),
                "BattleHud 未创建 HUD Canvas。");

            var clockLabel = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "TopLeft");
            Assert.IsNotNull(clockLabel, "HUD 顶栏 TopLeft 未创建。");

            // 跑一段真实帧：sim.Tick（固定步长积分）→ view.Sync → hud.Refresh 全部执行。
            float deadline = Time.realtimeSinceStartup + ObserveSeconds;
            int guard = 0;
            while (Time.realtimeSinceStartup < deadline && guard++ < 10000)
            {
                yield return null;
            }

            Assert.IsTrue(
                clockLabel.text.Contains("战役时间"),
                "HUD 顶栏未刷新为战役时间文案：" + clockLabel.text);

            var match = Regex.Match(clockLabel.text, @"战役时间\s*(\d+):(\d+)");
            Assert.IsTrue(match.Success, "HUD 顶栏时间格式异常：" + clockLabel.text);

            int elapsedSeconds = int.Parse(match.Groups[1].Value) * 60 + int.Parse(match.Groups[2].Value);
            Assert.IsTrue(
                elapsedSeconds > 0,
                $"战役时钟未推进（HUD 仍为 {match.Value}）——sim.Tick → Step → Clock.Advance 链路未跑通。");
        }
    }
}
