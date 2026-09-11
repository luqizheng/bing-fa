using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChinaBettle.Battle.Campaign
{
    /// <summary>
    /// 存档数据（GDD §5.4：JSON 格式存本地 / 未来 Steam Cloud）。
    ///
    /// 范围取舍：存档保存**战役进度与历史**（幕、编年史、复盘采样、双方部队与粮草），
    /// 不保存"某个战斗瞬间的完整世界状态"——单机战役是可重开的一场局，
    /// 而 GDD §5.4 要的核心是"历史事件以可回放编年史保存"。
    /// </summary>
    public sealed class BattleSave
    {
        /// <summary>存档 schema 版本（升级存档格式时必须递增，并在读取时按版本迁移）。</summary>
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        /// <summary>地图标识（SliceMaps 里的地图 Id）。</summary>
        public string MapId { get; set; } = string.Empty;

        /// <summary>战役时钟（战场秒）。</summary>
        public float ElapsedSeconds { get; set; }

        /// <summary>当前幕。</summary>
        public string CurrentAct { get; set; } = string.Empty;

        /// <summary>是否已结算。</summary>
        public bool Finished { get; set; }

        /// <summary>结算原因（未结算时为空）。</summary>
        public string OutcomeReason { get; set; } = string.Empty;

        /// <summary>谋略点。</summary>
        public int PlayerPoints { get; set; }

        /// <summary>部队快照。</summary>
        public List<UnitSave> Units { get; set; } = new();

        /// <summary>编年史。</summary>
        public List<ChronicleSave> Chronicle { get; set; } = new();
    }

    /// <summary>单位存档条目（只用可重建的必要字段）。</summary>
    public sealed class UnitSave
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Faction { get; set; } = string.Empty;

        public string DefinitionId { get; set; } = string.Empty;

        public float X { get; set; }

        public float Z { get; set; }

        public float Health { get; set; }

        public float RationsUnits { get; set; }

        public float Starvation { get; set; }

        public bool Formed { get; set; }
    }

    /// <summary>编年史条目（GDD §5.4 的可回放历史）。</summary>
    public sealed class ChronicleSave
    {
        public float AtSeconds { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// 存档序列化器（纯逻辑、可单测）。
    ///
    /// 为什么手写 JSON：Unity 的 <c>JsonUtility</c> 依赖 UnityEngine（Foundation 层禁用），
    /// 而 .NET Standard 2.1 的 Unity 侧没有 <c>System.Text.Json</c>。
    /// 故按已知 schema 手写一个**最小、可校验**的读写器——它只处理本文件的固定结构，
    /// 不做通用 JSON 解析，避免把解析器的复杂度与风险带进游戏核心。
    /// </summary>
    public static class BattleSaveSerializer
    {
        public static string Serialize(BattleSave save)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"version\": {save.Version},\n");
            sb.Append($"  \"mapId\": {Q(save.MapId)},\n");
            sb.Append($"  \"elapsedSeconds\": {N(save.ElapsedSeconds)},\n");
            sb.Append($"  \"currentAct\": {Q(save.CurrentAct)},\n");
            sb.Append($"  \"finished\": {(save.Finished ? "true" : "false")},\n");
            sb.Append($"  \"outcomeReason\": {Q(save.OutcomeReason)},\n");
            sb.Append($"  \"playerPoints\": {save.PlayerPoints},\n");

            sb.Append("  \"units\": [");
            for (int i = 0; i < save.Units.Count; i++)
            {
                var u = save.Units[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    {");
                sb.Append($"\"id\": {Q(u.Id)}, \"label\": {Q(u.Label)}, \"faction\": {Q(u.Faction)}, ");
                sb.Append($"\"definitionId\": {Q(u.DefinitionId)}, ");
                sb.Append($"\"x\": {N(u.X)}, \"z\": {N(u.Z)}, \"health\": {N(u.Health)}, ");
                sb.Append($"\"rations\": {N(u.RationsUnits)}, \"starvation\": {N(u.Starvation)}, ");
                sb.Append($"\"formed\": {(u.Formed ? "true" : "false")}");
                sb.Append("}");
            }

            sb.Append(save.Units.Count > 0 ? "\n  ],\n" : "],\n");

            sb.Append("  \"chronicle\": [");
            for (int i = 0; i < save.Chronicle.Count; i++)
            {
                var c = save.Chronicle[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append($"    {{\"at\": {N(c.AtSeconds)}, \"kind\": {Q(c.Kind)}, \"text\": {Q(c.Text)}}}");
            }

            sb.Append(save.Chronicle.Count > 0 ? "\n  ]\n" : "]\n");
            sb.Append("}\n");

            return sb.ToString();
        }

        /// <summary>
        /// 反序列化。**按固定 schema 顺序读取**（本序列化器只写这个顺序），
        /// 对缺字段容错为默认值，不抛异常——损坏的存档应降级为"读不出内容"而非崩溃。
        /// </summary>
        public static BattleSave Deserialize(string json)
        {
            var save = new BattleSave();

            if (string.IsNullOrWhiteSpace(json))
            {
                return save;
            }

            save.Version = (int)ReadNumber(json, "\"version\"", save.Version);
            save.MapId = ReadString(json, "\"mapId\"", string.Empty);
            save.ElapsedSeconds = ReadNumber(json, "\"elapsedSeconds\"", 0f);
            save.CurrentAct = ReadString(json, "\"currentAct\"", string.Empty);
            save.Finished = ReadBool(json, "\"finished\"", false);
            save.OutcomeReason = ReadString(json, "\"outcomeReason\"", string.Empty);
            save.PlayerPoints = (int)ReadNumber(json, "\"playerPoints\"", 0f);

            foreach (var obj in ReadArrayObjects(json, "\"units\""))
            {
                save.Units.Add(new UnitSave
                {
                    Id = ReadString(obj, "\"id\"", string.Empty),
                    Label = ReadString(obj, "\"label\"", string.Empty),
                    Faction = ReadString(obj, "\"faction\"", string.Empty),
                    DefinitionId = ReadString(obj, "\"definitionId\"", string.Empty),
                    X = ReadNumber(obj, "\"x\"", 0f),
                    Z = ReadNumber(obj, "\"z\"", 0f),
                    Health = ReadNumber(obj, "\"health\"", 0f),
                    RationsUnits = ReadNumber(obj, "\"rations\"", 0f),
                    Starvation = ReadNumber(obj, "\"starvation\"", 0f),
                    Formed = ReadBool(obj, "\"formed\"", false),
                });
            }

            foreach (var obj in ReadArrayObjects(json, "\"chronicle\""))
            {
                save.Chronicle.Add(new ChronicleSave
                {
                    AtSeconds = ReadNumber(obj, "\"at\"", 0f),
                    Kind = ReadString(obj, "\"kind\"", string.Empty),
                    Text = ReadString(obj, "\"text\"", string.Empty),
                });
            }

            return save;
        }

        // ───────────────────────── 最小 JSON 读写工具 ─────────────────────────

        /// <summary>数字一律用不变文化格式化，避免不同区域设置写出 "0,5" 这种非法 JSON。</summary>
        private static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Q(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (char ch in value ?? string.Empty)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(ch); break;
                }
            }

            return sb.Append('"').ToString();
        }

        private static string ReadString(string json, string key, string fallback)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
            {
                return fallback;
            }

            int colon = json.IndexOf(':', i);
            if (colon < 0)
            {
                return fallback;
            }

            int start = json.IndexOf('"', colon + 1);
            if (start < 0)
            {
                return fallback;
            }

            var sb = new StringBuilder();
            for (int p = start + 1; p < json.Length; p++)
            {
                char ch = json[p];
                if (ch == '\\' && p + 1 < json.Length)
                {
                    char next = json[++p];
                    sb.Append(next switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => next,
                    });
                    continue;
                }

                if (ch == '"')
                {
                    return sb.ToString();
                }

                sb.Append(ch);
            }

            return fallback;
        }

        private static float ReadNumber(string json, string key, float fallback)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
            {
                return fallback;
            }

            int colon = json.IndexOf(':', i);
            if (colon < 0)
            {
                return fallback;
            }

            int p = colon + 1;
            while (p < json.Length && (json[p] == ' ' || json[p] == '\t'))
            {
                p++;
            }

            int start = p;
            while (p < json.Length && (char.IsDigit(json[p]) || json[p] == '-' || json[p] == '+' || json[p] == '.' ||
                                       json[p] == 'e' || json[p] == 'E'))
            {
                p++;
            }

            if (p == start)
            {
                return fallback;
            }

            return float.TryParse(json.Substring(start, p - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        private static bool ReadBool(string json, string key, bool fallback)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
            {
                return fallback;
            }

            int colon = json.IndexOf(':', i);
            if (colon < 0)
            {
                return fallback;
            }

            int t = json.IndexOf("true", colon, StringComparison.Ordinal);
            int f = json.IndexOf("false", colon, StringComparison.Ordinal);

            if (t >= 0 && (f < 0 || t < f))
            {
                return true;
            }

            return f >= 0 ? false : fallback;
        }

        /// <summary>取出数组内每个 `{...}` 对象的文本（按括号配平，字符串内的括号不计）。</summary>
        private static IEnumerable<string> ReadArrayObjects(string json, string key)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0)
            {
                yield break;
            }

            int open = json.IndexOf('[', i);
            if (open < 0)
            {
                yield break;
            }

            int depth = 0;
            int objStart = -1;
            bool inString = false;

            for (int p = open + 1; p < json.Length; p++)
            {
                char ch = json[p];

                if (inString)
                {
                    if (ch == '\\')
                    {
                        p++;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{')
                {
                    if (depth == 0)
                    {
                        objStart = p;
                    }

                    depth++;
                    continue;
                }

                if (ch == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        yield return json.Substring(objStart, p - objStart + 1);
                        objStart = -1;
                    }

                    continue;
                }

                if (ch == ']' && depth == 0)
                {
                    yield break;
                }
            }
        }
    }
}
