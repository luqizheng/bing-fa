using System;
using System.IO;
using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Battle.Campaign;
using ChinaBettle.Foundation.Units;
using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>
    /// 存档落盘（GDD §5.4：JSON 存本地，未来接 Steam Cloud）。
    ///
    /// 分层纪律：**序列化在 Foundation/Battle 侧（纯逻辑、可单测）**，
    /// 本类只做"对象 ↔ 字节"与路径管理——这样存档格式的演进可脱离 Unity 验证。
    /// </summary>
    public static class BattleSaveStore
    {
        /// <summary>默认存档文件名（单机战役单槽；多槽位属路线图）。</summary>
        public const string FileName = "changping_save.json";

        /// <summary>存档目录：Application.persistentDataPath（跨平台可写位置）。</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>从仿真快照出存档对象。</summary>
        public static BattleSave Capture(BattleSimulation sim)
        {
            var save = new BattleSave
            {
                MapId = sim.Map.Id,
                ElapsedSeconds = sim.Clock.ElapsedSeconds,
                CurrentAct = sim.CurrentAct.ToString(),
                Finished = sim.IsFinished,
                OutcomeReason = sim.OutcomeReason,
                PlayerPoints = sim.PlayerPoints.Points,
            };

            foreach (var unit in sim.Units.Where(u => u.Alive))
            {
                save.Units.Add(new UnitSave
                {
                    Id = unit.Id,
                    Label = unit.DisplayLabel,
                    Faction = unit.Faction.ToString(),
                    DefinitionId = unit.Definition.Id,
                    X = unit.Position.X,
                    Z = unit.Position.Z,
                    Health = unit.Health,
                    RationsUnits = unit.RationsUnits,
                    Starvation = unit.Starvation,
                    Formed = unit.Formed,
                });
            }

            foreach (var entry in sim.Chronicle)
            {
                save.Chronicle.Add(new ChronicleSave
                {
                    AtSeconds = entry.AtSeconds,
                    Kind = entry.Kind.ToString(),
                    Text = entry.Text,
                });
            }

            return save;
        }

        /// <summary>写入磁盘。返回是否成功（失败不抛异常——存档失败不应中断游戏）。</summary>
        public static bool Write(BattleSimulation sim)
        {
            try
            {
                string path = SavePath;
                string json = BattleSaveSerializer.Serialize(Capture(sim));
                File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                Debug.Log($"[ChinaBettle] 已存档：{path}（{json.Length} 字节）");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChinaBettle] 存档失败：{e.Message}");
                return false;
            }
        }

        /// <summary>从磁盘读取。无存档或损坏时返回 null（调用方按"无存档"处理）。</summary>
        public static BattleSave? Read()
        {
            try
            {
                string path = SavePath;
                if (!File.Exists(path))
                {
                    return null;
                }

                return BattleSaveSerializer.Deserialize(File.ReadAllText(path, System.Text.Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChinaBettle] 读档失败：{e.Message}");
                return null;
            }
        }

        /// <summary>是否存在存档。</summary>
        public static bool Exists() => File.Exists(SavePath);

        /// <summary>删除存档（调试与"新开一局"用）。</summary>
        public static bool Delete()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChinaBettle] 删档失败：{e.Message}");
                return false;
            }
        }
    }
}
