using System.Collections.Generic;
using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Stratagems;
using ChinaBettle.Foundation.Trust;
using ChinaBettle.Foundation.Units;
using UnityEngine;

namespace ChinaBettle.Game
{
    /// <summary>
    /// 切片主控制器：组装仿真 + 3D 表现 + HUD，处理玩家输入，逐帧驱动战役时钟。
    ///
    /// 职责边界：本类只做"输入 → 指令"与"状态 → 表现"的搬运；
    /// 全部规则结算在 <see cref="BattleSimulation"/>（Foundation 纯逻辑），HUD 只渲染。
    /// 键位见 UI-01：F1–F4 技能槽、1/2/3 幕僚建议、Ctrl/Alt+数字 编队。
    /// </summary>
    public sealed class SliceGame : MonoBehaviour
    {
        private BattleSimulation sim = null!;
        private BattleView view = null!;
        private BattleHud hud = null!;
        private Camera cam = null!;

        private readonly HashSet<string> selected = new();
        private readonly Dictionary<int, List<string>> groups = new();
        private readonly AdvisorSignal advisorSignal = new();

        private StratagemId? aimingSkill;
        private int highlightedAdvisor; // 1=廉颇 2=赵括 3=苏代（切片灰显）
        private string toast = string.Empty;
        private float toastUntil;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            cam = CreateCamera();
            CreateLight();

            sim = new BattleSimulation(new BattleRules(), autoPlayAi: true);

            var root = new GameObject("SliceWorld").transform;
            root.SetParent(transform, worldPositionStays: false);

            view = new BattleView(sim, root);
            view.SetCamera(cam);
            hud = new BattleHud(transform);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Restart();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (sim.Clock.IsPaused)
                {
                    sim.Clock.Resume();
                }
                else
                {
                    sim.Clock.Pause();
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                aimingSkill = null;
            }

            sim.Tick(Time.deltaTime);

            HandleSkillInput();
            HandleOrderInput();
            HandleGroupInput();
            HandleCamera();
            HandleAdvisorInput();

            view.Sync(sim.Clock.ElapsedSeconds, selected);
            hud.Refresh(BuildHudModel());
        }

        private void Restart()
        {
            selected.Clear();
            groups.Clear();
            aimingSkill = null;
            highlightedAdvisor = 0;

            foreach (Transform child in transform)
            {
                if (child.name is "SliceWorld" or "HudCanvas")
                {
                    Destroy(child.gameObject);
                }
            }

            sim = new BattleSimulation(new BattleRules(), autoPlayAi: true);
            var root = new GameObject("SliceWorld").transform;
            root.SetParent(transform, worldPositionStays: false);
            view = new BattleView(sim, root);
            view.SetCamera(cam);
            hud = new BattleHud(transform);
            Toast("新战役开始");
        }

        // ───────────────────────────── 输入 ─────────────────────────────

        private void HandleSkillInput()
        {
            if (sim.IsFinished)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                BeginSkill(StratagemId.ReduceStove);
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                BeginSkill(StratagemId.AddStove);
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                BeginSkill(StratagemId.FireAttack);
            }
            else if (Input.GetKeyDown(KeyCode.F4))
            {
                Toast("第 4 槽为幕僚专属计（SKILL-08/09），切片未实现");
            }

            if (aimingSkill is null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && view.TryPickGround(Input.mousePosition, out var point))
            {
                var skill = aimingSkill.Value;
                bool ok = sim.TryCast(Faction.Zhao, skill, point);
                Toast(ok ? $"已施放：{SkillName(skill)}" : $"施放失败：{SkillName(skill)}（见编年史）");
                aimingSkill = null;
            }
        }

        private void BeginSkill(StratagemId id)
        {
            if (!sim.CanCast(Faction.Zhao, id, out string reason))
            {
                Toast($"无法施放 {SkillName(id)}：{reason}");
                return;
            }

            // 有选中部队时：直接把计施在自己部队所在区域（减灶/增灶=掩盖己方实力），省一次点击。
            var selectedUnits = SelectedUnits().ToList();
            if (selectedUnits.Count > 0)
            {
                var centroid = new MapPoint(
                    selectedUnits.Average(u => u.Position.X),
                    selectedUnits.Average(u => u.Position.Z));
                bool ok = sim.TryCast(Faction.Zhao, id, centroid);
                Toast(ok ? $"已施放：{SkillName(id)}（于选中部队所在区域）" : "施放失败（见编年史）");
                return;
            }

            aimingSkill = id;
            Toast($"选择 {SkillName(id)} 的施放区域：左键确认 / Esc 取消", 6f);
        }

        private void HandleOrderInput()
        {
            if (aimingSkill is not null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (view.TryPickUnit(Input.mousePosition, out string unitId))
                {
                    var unit = sim.Units.FirstOrDefault(u => u.Id == unitId);
                    if (unit is not null && unit.Faction == Faction.Zhao)
                    {
                        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                        {
                            selected.Clear();
                        }

                        selected.Add(unitId);
                    }
                    else if (unit is not null)
                    {
                        selected.Clear();
                        foreach (var own in sim.Units.Where(u => u.Faction == Faction.Zhao && u.Alive))
                        {
                            selected.Add(own.Id);
                        }

                        foreach (var id in selected)
                        {
                            sim.OrderAttack(id, unitId);
                        }

                        Toast($"全军攻击 {unit.DisplayLabel}");
                    }
                }
                else if (!Input.GetKey(KeyCode.LeftShift))
                {
                    selected.Clear();
                }
            }

            if (Input.GetMouseButtonDown(1) && selected.Count > 0 &&
                view.TryPickGround(Input.mousePosition, out var ground))
            {
                if (view.TryPickUnit(Input.mousePosition, out string targetId))
                {
                    var target = sim.Units.FirstOrDefault(u => u.Id == targetId);
                    if (target is not null && target.Faction != Faction.Zhao)
                    {
                        foreach (var id in selected)
                        {
                            sim.OrderAttack(id, targetId);
                        }

                        Toast($"命令攻击 {target.DisplayLabel}");
                        return;
                    }
                }

                foreach (var id in selected)
                {
                    sim.OrderMove(id, ground);
                }

                Toast($"移动至 ({ground.X:0}, {ground.Z:0})");
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                foreach (var unit in SelectedUnits().ToList())
                {
                    sim.ToggleFormation(unit.Id);
                }
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                foreach (var id in selected)
                {
                    sim.OrderStop(id);
                }

                Toast("原地待命");
            }
        }

        private void HandleGroupInput()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            for (int i = 1; i <= 5; i++)
            {
                var key = KeyCode.Alpha0 + i;
                if (ctrl && Input.GetKeyDown(key))
                {
                    groups[i] = selected.ToList();
                    Toast($"编队 {i} 已保存（{selected.Count} 队）");
                }
                else if (alt && Input.GetKeyDown(key) && groups.TryGetValue(i, out var group))
                {
                    selected.Clear();
                    foreach (var id in group)
                    {
                        selected.Add(id);
                    }

                    Toast($"召回编队 {i}");
                }
            }
        }

        private void HandleAdvisorInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                highlightedAdvisor = 1;
                Toast($"廉颇：{AdviceOf(AdvisorProfile.LianPo)}（谨慎 9，倾向低估时机）");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                highlightedAdvisor = 2;
                Toast($"赵括：{AdviceOf(AdvisorProfile.ZhaoKuo)}（激进，倾向高估时机）");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                toast = "苏代：切片未实装（二期 ADV 系列）";
                toastUntil = Time.time + 3f;
            }
        }

        private void HandleCamera()
        {
            float speed = 60f * Time.deltaTime * (cam.orthographicSize / 60f);
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                move += Vector3.forward;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                move += Vector3.back;
            }

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                move += Vector3.left;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                move += Vector3.right;
            }

            cam.transform.position += move * speed;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * 8f, 35f, 170f);
            }
        }

        // ───────────────────────────── 表现数据 ─────────────────────────────

        private HudModel BuildHudModel()
        {
            return new HudModel
            {
                ElapsedSeconds = sim.Clock.ElapsedSeconds,
                RemainingSeconds = sim.RemainingSeconds,
                Points = sim.PlayerPoints.Points,
                Paused = sim.Clock.IsPaused,
                Finished = sim.IsFinished,
                OutcomeTitle = sim.Outcome switch
                {
                    BattleOutcome.ZhaoVictory => "赵 军 胜",
                    BattleOutcome.QinVictory => "秦 军 胜",
                    BattleOutcome.Draw => "平 局",
                    _ => string.Empty,
                },
                OutcomeReason = sim.OutcomeReason,
                StatsLine =
                    $"歼敌：赵 {sim.Stats.ZhaoKills} / 秦 {sim.Stats.QinKills}　" +
                    $"损失：赵 {sim.Stats.ZhaoLost} / 秦 {sim.Stats.QinLost}　" +
                    $"识破：赵 {sim.Stats.PlayerDetections} / 秦 {sim.Stats.AiDetections}　" +
                    $"施计：赵 {sim.Stats.PlayerCasts} / 秦 {sim.Stats.AiCasts}",
                Feed = sim.PlayerIntelFeed(sim.Clock.ElapsedSeconds),
                Chronicle = sim.Chronicle,
                Skills = BuildSkillSlots(),
                SelectedLines = BuildSelectedLines(),
                AimPrompt = aimingSkill is null
                    ? string.Empty
                    : $"施计瞄准：{SkillName(aimingSkill.Value)} —— 左键点击目标区域（Esc 取消）",
                Advisors = BuildAdvisorLines(),
                GroupHint = Time.time < toastUntil ? toast : string.Empty,
            };
        }

        private List<SkillSlotInfo> BuildSkillSlots()
        {
            var slots = new List<SkillSlotInfo>();
            var ordered = new[]
            {
                (KeyCode.F1, StratagemId.ReduceStove, "减灶示弱"),
                (KeyCode.F2, StratagemId.AddStove, "增灶示强"),
                (KeyCode.F3, StratagemId.FireAttack, "火攻粮道"),
            };

            foreach (var (key, id, name) in ordered)
            {
                var definition = id switch
                {
                    StratagemId.ReduceStove => StratagemDefinition.ReduceStove,
                    StratagemId.AddStove => StratagemDefinition.AddStove,
                    _ => StratagemDefinition.FireAttack,
                };

                bool canCast = sim.CanCast(Faction.Zhao, id, out string reason);
                float cooldown = sim.CooldownRemaining(Faction.Zhao, id);
                string state = canCast ? "就绪" : (cooldown > 0f ? $"冷却 {cooldown:0}s" : "不可用");
                slots.Add(new SkillSlotInfo(key.ToString(), name, definition.StrategyPointCost, state, canCast));
            }

            return slots;
        }

        private List<string> BuildSelectedLines()
        {
            var lines = new List<string>();
            foreach (var unit in SelectedUnits().Take(5))
            {
                lines.Add(
                    $"{unit.DisplayLabel}　{unit.Definition.DisplayName}　" +
                    $"兵 {unit.HealthRatio * 100f:0}%　士气 {unit.Morale.Value}（{MoraleName(unit.MoraleState)}）" +
                    $"{(unit.Formed ? "　【结阵】" : string.Empty)}" +
                    $"{(unit.Starvation > 0f ? $"　饥饿 {unit.Starvation * 100f:0}%" : string.Empty)}");
            }

            if (selected.Count > 5)
            {
                lines.Add($"…共选中 {selected.Count} 队");
            }

            return lines;
        }

        private List<AdvisorLine> BuildAdvisorLines()
        {
            return new List<AdvisorLine>
            {
                new("廉颇", "谨慎 9", AdviceOf(AdvisorProfile.LianPo), highlightedAdvisor == 1),
                new("赵括", "激进 9", AdviceOf(AdvisorProfile.ZhaoKuo), highlightedAdvisor == 2),
                new("苏代", "奇谋 8", "切片未实装（二期）", false),
            };
        }

        /// <summary>
        /// 幕僚代理信号（TRUST-10）：幕僚读的是【AI 对玩家的信任桶】（AI 内部状态），
        /// 只输出"施计时机"档位、不暴露桶值。桶值取自 AI 视角关于赵军兵力的主题。
        /// </summary>
        private string AdviceOf(AdvisorProfile profile)
        {
            float bucket = sim.AiTrust.Get(sim.AiTopic);
            var advice = advisorSignal.Evaluate(profile, bucket);
            return advice switch
            {
                AdvisorAdvice.Advisable => "可施计",
                AdvisorAdvice.Hesitant => "勉强，恐引复核",
                _ => "时机未到，先养",
            };
        }

        private IEnumerable<SimUnit> SelectedUnits() =>
            sim.Units.Where(u => u.Alive && selected.Contains(u.Id));

        private void Toast(string message, float seconds = 3f)
        {
            toast = message;
            toastUntil = Time.time + seconds;
        }

        private static string SkillName(StratagemId id) => id switch
        {
            StratagemId.ReduceStove => "减灶示弱",
            StratagemId.AddStove => "增灶示强",
            StratagemId.FireAttack => "火攻粮道",
            _ => id.ToString(),
        };

        private static string MoraleName(MoraleState state) => state switch
        {
            MoraleState.High => "高昂",
            MoraleState.Normal => "正常",
            MoraleState.Shaken => "动摇",
            _ => "崩溃",
        };

        // ───────────────────────────── 场景构建 ─────────────────────────────

        private static Camera CreateCamera()
        {
            var existing = Camera.main;
            if (existing is not null)
            {
                Configure(existing);
                return existing;
            }

            var go = new GameObject("MainCamera", typeof(Camera));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            Configure(camera);
            return camera;
        }

        private static void Configure(Camera camera)
        {
            camera.orthographic = true;
            camera.orthographicSize = 90f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 800f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SlicePalette.CameraBackground;
            camera.transform.position = new Vector3(0f, 150f, -80f);
            camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        }

        private static void CreateLight()
        {
            if (Object.FindFirstObjectByType<Light>() is not null)
            {
                return;
            }

            var go = new GameObject("DirectionalLight", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }
    }

}