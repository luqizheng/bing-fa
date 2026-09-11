using System;
using System.Collections.Generic;
using System.Linq;
using ChinaBettle.AI;
using ChinaBettle.Foundation.Combat;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Stratagems;
using ChinaBettle.Foundation.Time;
using ChinaBettle.Foundation.Trust;
using ChinaBettle.Foundation.Units;
using ChinaBettle.Intel;
using ChinaBettle.Stratagems;
using ChinaBettle.Units;

namespace ChinaBettle.Battle
{
    /// <summary>
    /// 垂直切片战役仿真（SLICE-01~04，无头、纯 C#、可 dotnet 测试）。
    ///
    /// 覆盖：双军编制与移动（地形/结阵/士气/饥饿修正）、克制环接战与结阵反噬、火区灼烧、
    /// 士气四档与溃逃、携粮与饥饿三档、三技能（减灶/增灶/火攻）施放与冷却、
    /// 斥候目视情报生成 + 欺骗痕迹篡改 + 近距识破 + 信任桶双向更新、
    /// TRUST-07 强复核冻结、AI-05 AI 欺骗行为、胜负判定与编年史。
    ///
    /// 时钟纪律（TIME-02）：所有结算吃战役时钟（<see cref="BattleClock"/>），暂停即冻结；
    /// 不读 UnityEngine.Time。表现层只读本类状态快照，不下场算规则。
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly List<SimUnit> units = new();
        private readonly List<FireZone> fireZones = new();
        private readonly List<ChronicleEntry> chronicle = new();
        private readonly List<(Faction Owner, DeceptionTrace Trace)> deceptionTraces = new();
        private readonly Dictionary<StratagemId, float> playerLastCast = new();
        private readonly Dictionary<StratagemId, float> aiLastCast = new();
        private readonly BattleStats stats = new();
        private readonly CounterRing counterRing;

        private readonly CampaignRunner campaign = new();

        private float observationAccumulator;
        private float supplyAccumulator;
        private float aiThinkAccumulator;
        private float aiFreezeUntilSeconds = -1f;
        private int intelSequence;
        private float lastPayloadAiSaw = -1f;
        private AiPosture aiPosture = AiPosture.Hold;
        private bool zhaoSortedOut;
        private float supplyPathAccumulator;
        private bool zhaoPathOpen = true;
        private float siegeSeconds;
        private bool siegeAnnounced;
        private bool siegeAmplified;
        private int initialZhaoCombatUnits;

        public BattleSimulation(BattleRules? rules = null, bool autoPlayAi = true, BattleMapDefinition? map = null)
        {
            Rules = rules ?? new BattleRules();
            Map = map ?? SliceMaps.Default;
            Clock = new BattleClock();
            counterRing = new CounterRing(Rules.Combat);
            AiBrain = new AiBrain(this);
            AutoPlayAi = autoPlayAi;

            DeployInitialForces();

            // 剧本基线（CP-06③ 出垒比例的分母）与开局幕。
            initialZhaoCombatUnits = System.Math.Max(1, units.Count(u => u.Faction == Faction.Zhao && !u.IsScout));

            // 开局推进第 ① 幕；**必须消费事件**，否则幕进入文案不会进编年史（复盘缺幕）。
            foreach (var e in campaign.Step(BuildCampaignContext(0f)).Events)
            {
                ApplyCampaignEvent(e);
            }

            Log(ChronicleKind.Outcome,
                $"战役开始：{Map.DisplayName}。单局上限 20 战场分钟（TIME-04）。");
        }

        public BattleRules Rules { get; }

        public BattleMapDefinition Map { get; }

        public BattleClock Clock { get; }

        /// <summary>AI 侧决策（情报门控 + 欺骗 + 军事态势）。</summary>
        public AiBrain AiBrain { get; }

        public bool AutoPlayAi { get; set; }

        public IReadOnlyList<SimUnit> Units => units;

        public IReadOnlyList<MapProp> Props => Map.Props;

        public IReadOnlyList<FireZone> FireZones => fireZones;

        public IReadOnlyList<ChronicleEntry> Chronicle => chronicle;

        public BattleStats Stats => stats;

        public StrategyPointWallet PlayerPoints { get; } = new();

        public StrategyPointWallet AiPoints { get; } = new();

        /// <summary>玩家侧情报池（接收方视角；认知查询只走这里，NET-02/SLICE-06）。</summary>
        public IntelPool PlayerIntel { get; } = new();

        /// <summary>AI 侧情报池。</summary>
        public IntelPool AiIntel { get; } = new();

        /// <summary>AI 对玩家的信任桶（TRUST-01：AI 内部状态，玩家不可见，只经幕僚代理信号间接感知）。</summary>
        public TrustBucketSystem AiTrust { get; } = new();

        /// <summary>玩家（赵）对秦军的信任桶。玩家侧不使用信任桶做决策，此处仅供剧本与复盘呈现（TRUST-10 不暴露数值）。</summary>
        public TrustBucketSystem PlayerTrust { get; } = new();

        /// <summary>长平剧本运行器（CP-01…08）：幕状态、幕历史与结局。</summary>
        public CampaignRunner Campaign => campaign;

        /// <summary>当前幕（HUD 显示）。</summary>
        public CampaignAct CurrentAct => campaign.Current;

        /// <summary>当前幕标题。</summary>
        public string CurrentActTitle => campaign.CurrentTitle;

        /// <summary>秦军当前态势（由剧本幕驱动，供 AI 态势决策消费）。</summary>
        public AiPosture AiPosture => aiPosture;

        /// <summary>权威端欺骗元数据（单机=本地权威侧，未来=服务端；接收方查询拿不到，NET-02）。</summary>
        public List<AuthoritativeIntel> AuthoritativeRecords { get; } = new();

        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Undecided;

        public string OutcomeReason { get; private set; } = string.Empty;

        public bool IsFinished => Outcome != BattleOutcome.Undecided;

        public float RemainingSeconds => MathF.Max(0f, Rules.TimeLimitSeconds - Clock.ElapsedSeconds);

        /// <summary>玩家（赵）关心的情报主题 = 秦军兵力；AI 关心的 = 赵军兵力。</summary>
        public string PlayerTopic => "秦军_兵力";

        public string AiTopic => "赵军_兵力";

        // ───────────────────────────── 部署 ─────────────────────────────

        private void DeployInitialForces()
        {
            // 部署数据来自地图定义（真源 CP-05；滏口陉图等价于原硬编码部署）。
            foreach (var deployment in Map.Deployments)
            {
                Add(deployment.Definition, Map.ClampToBounds(deployment.Position), deployment.Label);
            }
        }

        private void Add(UnitDefinition definition, MapPoint position, string label)
        {
            var faction = label.StartsWith("赵") ? Faction.Zhao : Faction.Qin;
            var unit = new SimUnit($"{faction}_{units.Count:D2}", faction, definition, position, Rules)
            {
                DisplayLabel = label,
            };
            units.Add(unit);
        }

        // ───────────────────────────── 主循环 ─────────────────────────────

        /// <summary>推进战役时钟与全部战役层系统（TIME-02：暂停由时钟冻结承载）。</summary>
        public void Tick(float deltaSeconds)
        {
            if (IsFinished || deltaSeconds <= 0f || Clock.IsPaused)
            {
                return;
            }

            // 固定步长积分：表现层帧率不可信，规则结算必须与帧率无关。
            const float FixedStep = 0.1f;
            float remaining = deltaSeconds;
            while (remaining > 0f && !IsFinished)
            {
                float step = MathF.Min(FixedStep, remaining);
                Step(step);
                remaining -= step;
            }
        }

        private void Step(float step)
        {
            Clock.Advance(step);
            float now = Clock.ElapsedSeconds;

            TickCombat(step, now);
            TickMovement(step);
            TickFireZones(step, now);
            TickSupply(step);
            TickObservation(step, now);
            TickCapture(step);

            if (AutoPlayAi)
            {
                aiThinkAccumulator += step;
                if (aiThinkAccumulator >= Rules.AiThinkIntervalSeconds)
                {
                    aiThinkAccumulator = 0f;
                    AiBrain.Think(now);
                }
            }

            // 顺序契约：**先战术结算，后剧本结算**。
            // 歼灭/焚粮是可观测的战术事实（GDD §2.7.1 前三条），优先于剧本的态势性结局（CP-08）；
            // 反之会让"全歼秦军"被改写成"秦军退却"，结算原因失真。
            EvaluateOutcome();
            TickCampaign(now);
        }

        // ───────────────────────────── 剧本（CP-01…08）─────────────────────────────

        /// <summary>
        /// 推进长平剧本：组装只读快照 → 运行器 Step → 执行事件流。
        /// 契约：剧本**不直接改战场**，一切效果经本方法落到仿真（见 <see cref="CampaignEvent"/>）。
        /// </summary>
        private void TickCampaign(float now)
        {
            if (IsFinished)
            {
                return;
            }

            var result = campaign.Step(BuildCampaignContext(now));

            foreach (var e in result.Events)
            {
                ApplyCampaignEvent(e);
            }
        }

        /// <summary>组装剧本所需的只读快照（不含任何真值字段，NET-02）。</summary>
        private CampaignContext BuildCampaignContext(float now)
        {
            var zhaoCombat = units.Where(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout).ToList();
            var qinCombat = units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsScout).ToList();

            // 出垒判定（CP-06③）：壁垒线所属方（赵）位于【己方一侧以外】的单位即已出垒。
            int beyondWall = 0;
            var wall = Map.Fortifications.FirstOrDefault();
            if (wall is not null)
            {
                // 赵军在北、壁垒朝南：赵军越过壁垒线（X 向）即已出垒。
                beyondWall = zhaoCombat.Count(u => u.Position.X > wall.FixedX + BattleMapDefinition.FortificationThickness * 0.5f);
            }
            else
            {
                // 无壁垒的图（教学序章）以"离开本阵 60m 以上"近似表达出垒。
                beyondWall = zhaoCombat.Count(u => u.Position.DistanceTo(Map.BaseOf(Faction.Zhao)) > 60f);
            }

            bool qinHoldsFord = Map.Volumes
                .Where(v => v.Terrain == Foundation.Map.TerrainClass.Passable)
                .Where(v => Map.Volumes.Any(big => big.Terrain == Foundation.Map.TerrainClass.Impassable &&
                                                   System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                   System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .Any(ford => qinCombat.Any(u => u.Position.DistanceTo(ford.Center) <= 30f));

            bool corridorCut = qinCombat.Count(u => Map.AreaIdAt(u.Position) == "crossing") >= 2;

            return new CampaignContext
            {
                ElapsedSeconds = now,
                TimeLimitSeconds = Rules.TimeLimitSeconds,
                ZhaoUnits = zhaoCombat.Count,
                QinUnits = qinCombat.Count,
                ZhaoInitialUnits = initialZhaoCombatUnits,
                ZhaoUnitsBeyondWall = beyondWall,
                QinHoldsFord = qinHoldsFord,
                QinCutSupplyCorridor = corridorCut && campaign.Current >= CampaignAct.Encirclement,
                ZhaoGranaryLost = Map.GranariesOf(Faction.Zhao).All(g => g.IsBurned),
                ZhaoCampLost = false,
                AiTrustOnZhao = AiTrust.Get(AiTopic),
                PlayerTrustOnQin = PlayerTrust.Get(PlayerTopic),
                ZhaoBrokeOut = DetermineBreakout(now),
                ZhaoSortedOut = zhaoSortedOut,
            };
        }

        /// <summary>执行剧本事件（唯一的效果落地点）。</summary>
        private void ApplyCampaignEvent(CampaignEvent e)
        {
            switch (e)
            {
                case CampaignActEntered entered:
                    Log(ChronicleKind.Campaign, $"{entered.Title}｜{entered.OpeningText}");
                    break;

                case NarrativeIntelInject inject:
                    InjectNarrativeIntel(inject);
                    break;

                case SortieOrder sortie:
                    zhaoSortedOut = true;
                    OrderZhaoSortie();
                    Log(ChronicleKind.Campaign, sortie.Text);
                    break;

                case OffensiveOrder offensive:
                    aiPosture = AiPosture.Offensive;
                    Log(ChronicleKind.Campaign, offensive.Text);
                    break;

                case HoldOrder hold:
                    // 围困期的"转入封锁"＝**持续合围**，不是撤围回守势线。
                    // 若此处退回 Hold，合围态势当帧即被撤销（CP-06 的本意是就地封锁、绝其粮道）。
                    aiPosture = aiPosture == AiPosture.Encircle ? AiPosture.Encircle : AiPosture.Hold;
                    Log(ChronicleKind.Campaign, hold.Text);
                    break;

                case EncircleOrder encircle:
                    aiPosture = AiPosture.Encircle;
                    Log(ChronicleKind.Campaign, encircle.Text);
                    break;

                case CampaignEnd end:
                    Log(ChronicleKind.Campaign, end.Text);
                    Finish(end.PlayerWins ? BattleOutcome.ZhaoVictory : BattleOutcome.QinVictory,
                        $"{end.EndingId}（CP-08）");
                    break;

                default:
                    Log(ChronicleKind.Campaign, e.Text);
                    break;
            }
        }

        /// <summary>
        /// 执行赵军出垒（CP-02 ④）：赵括代廉颇后主动出击，**追击佯退的秦军**——
        /// 注意不是直撞秦军守势线（那是送死，且不合史实："秦军佯败而走，赵军悉众追之"）。
        ///
        /// 落点：丹水渡口以北的追击展开区（赵军出垒、渡河、追击，随即被合围）。
        /// 队伍保持梯次：弩兵在后、骑兵两翼、步兵居中——出垒后阵形不如壁垒内稳固，这是 CP-06 的伏笔。
        /// </summary>
        private void OrderZhaoSortie()
        {
            // 追击目标＝丹水渡口（秦军佯退方向），而非秦军守势线。
            var ford = Map.Volumes
                .Where(v => v.Terrain == Foundation.Map.TerrainClass.Passable)
                .Where(v => Map.Volumes.Any(big => big.Terrain == Foundation.Map.TerrainClass.Impassable &&
                                                   System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                   System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .OrderBy(v => v.Width * v.Depth)
                .FirstOrDefault();

            MapPoint axis = ford?.Center ?? Map.AiHoldLine;

            // 落点纪律：**停在丹水北岸**，不越过渡口。
            // 史实中赵军是被"佯退"逐步引过河的；剧本只负责下令出垒（脱离壁垒），
            // 越河追击的过程应由战场态势自然发生。若一次性把目标设到秦军守势线附近，
            // 赵军会径直撞进敌阵——第 ④ 幕就崩，后面的合围/断粮幕无从谈起。
            float northBank = axis.Z - 60f;

            var zhaoCombat = units.Where(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout).ToList();
            for (int i = 0; i < zhaoCombat.Count; i++)
            {
                var unit = zhaoCombat[i];
                float standoff = unit.Definition.HasRangedAttack ? -30f : (unit.IsCavalry ? -10f : 0f);

                unit.MoveGoal = Map.ClampToBounds(new MapPoint(
                    axis.X + (i - zhaoCombat.Count / 2f) * 12f,
                    northBank + standoff));
            }
        }

        /// <summary>
        /// 剧本叙事载荷注入：进入接收方情报池，按正常可信度公式与权重参与综合计算（情报规格 §3）。
        /// **不是欺骗技能产出**——不走 DECP-01 篡改系数、不生成痕迹、不可识破（真源 SKILL-13）。
        /// </summary>
        private void InjectNarrativeIntel(NarrativeIntelInject inject)
        {
            float baseCredibility = inject.SourceKind switch
            {
                Foundation.Intel.IntelSourceType.CapturedDocument => Rules.Credibility.CapturedDocumentBase,
                Foundation.Intel.IntelSourceType.Prisoner => Rules.Credibility.PrisonerBase,
                Foundation.Intel.IntelSourceType.SkillReveal => Rules.Credibility.SkillRevealBase,
                Foundation.Intel.IntelSourceType.PatternInference => Rules.Credibility.PatternBaseMax,
                _ => Rules.Credibility.ScoutVisualBase,
            };

            float fresh = CredibilityCalculator.Fresh(Rules.Credibility, inject.SourceKind, 0, 0f);
            var record = new IntelRecord
            {
                IntelId = $"INTEL_NARRATIVE_{++intelSequence:D4}",
                Topic = inject.Topic,
                SourceType = inject.SourceKind,
                BaseCredibility = baseCredibility,
                RawContent = new IntelPayload(IntelPayloadType.TroopCountEstimate, inject.Value),
                ScoutCount = 0,
                ObservationMinutes = 0f,
                CreatedAtSeconds = Clock.ElapsedSeconds,
            };
            record.Initialize(fresh);

            var recipientPool = inject.Recipient == Faction.Zhao ? PlayerIntel : AiIntel;
            recipientPool.Add(record);

            Log(ChronicleKind.Intel,
                $"{FactionName(inject.Recipient)}军收到（{inject.SourceKind}）：{inject.Topic} 载荷 {inject.Value:0} 人" +
                $"（可信度 {fresh:0}%，叙事事件非伪造载荷）");
        }

        /// <summary>突围判定（CP-08②）：进入突围幕后，赵军主力成建制脱离合围——以"渡过丹水且存活"表达。</summary>
        private bool DetermineBreakout(float now)
        {
            if (campaign.Current != CampaignAct.Breakout)
            {
                return false;
            }

            // 丹水以南（Z 大于河道带）的存活赵军战斗单位 ≥ 2 队即视为突围成功。
            var river = Map.Volumes
                .Where(v => v.Terrain == Foundation.Map.TerrainClass.Impassable && v.Width > v.Depth)
                .OrderByDescending(v => v.Width * v.Depth)
                .FirstOrDefault();
            if (river is null)
            {
                return false;
            }

            float southEdge = river.Center.Z + river.Depth * 0.5f;
            return units.Count(u => u.Alive && u.Faction == Faction.Zhao && !u.IsScout && u.Position.Z > southEdge) >= 2;
        }

        // ───────────────────────────── 接战 ─────────────────────────────

        private void TickCombat(float step, float now)
        {
            foreach (var attacker in units.Where(u => u.Alive).ToList())
            {
                attacker.AttackCooldownSeconds = MathF.Max(0f, attacker.AttackCooldownSeconds - step);

                if (attacker.IsRouted)
                {
                    continue; // 溃逃单位不作战（COMBAT-09）
                }

                var target = ResolveTarget(attacker);
                if (target is null || !target.Alive)
                {
                    continue;
                }

                float distance = attacker.Position.DistanceTo(target.Position);
                bool ranged = attacker.Definition.HasRangedAttack && distance > attacker.Definition.AttackRange;
                float range = ranged ? attacker.Definition.RangedRange : attacker.Definition.AttackRange;
                if (distance > range || attacker.AttackCooldownSeconds > 0f)
                {
                    continue;
                }

                attacker.AttackCooldownSeconds = ranged
                    ? attacker.Definition.RangedIntervalSeconds
                    : attacker.Definition.AttackIntervalSeconds;

                float coefficient = counterRing.Coefficient(attacker.Definition.Class, target.Definition.Class);
                // COMBAT-18③：守方位于己方壁垒缺口段时防御力 ×(1+加成)；攻方不享受。
                float defense = target.Definition.Defense *
                                (1f + Map.DefenderDefenseBonusAt(target.Position, target.Faction));
                int damage = DamageFormula.Resolve(
                    panelAttack: ranged ? attacker.Definition.RangedAttack : attacker.PanelAttack(Rules),
                    defense: defense,
                    counterCoefficient: coefficient,
                    morale: attacker.MoraleState,
                    terrainCoefficient: 1f, // 地形对射程的修正仍待兵种表 v0.4（COMBAT-16/18 已含减速与壁垒防御）
                    config: Rules.Combat);

                if (damage <= 0)
                {
                    continue;
                }

                target.Health -= damage;

                // COMBAT-10：被克制兵种命中 → 士气 −5/次。
                if (CounterRing.Counters(attacker.Definition.Class, target.Definition.Class))
                {
                    target.Morale.OnHitByCounterUnit();
                    if (target.IsRouted)
                    {
                        Log(ChronicleKind.Morale, $"{target.DisplayLabel} 士气崩溃，向本阵溃逃（COMBAT-09）");
                    }
                }

                // COMBAT-17③：拒马反噬——骑兵对【结阵枪兵】骑射/远程攻击时自身承伤 6 点/发。
                int reflect = FormationRules.CavalryReflectDamage(
                    attackerIsCavalry: attacker.IsCavalry,
                    defenderFormed: target.Formed && target.Definition.SpearFormationCapable,
                    config: Rules.Combat);
                if (reflect > 0)
                {
                    attacker.Health -= reflect;
                    Log(ChronicleKind.Combat,
                        $"{attacker.DisplayLabel} 骑射结阵 {target.DisplayLabel}，遭拒马反噬 {reflect} 点（COMBAT-17）");
                }

                Log(ChronicleKind.Combat,
                    $"{attacker.DisplayLabel} → {target.DisplayLabel}：{damage} 点（克制 ×{coefficient:0.00}），" +
                    $"{target.DisplayLabel} 余 {MathF.Max(0f, target.Health):0} 血");

                if (target.Health <= 0f)
                {
                    attacker.RegisterKill();
                    RemoveUnit(target, $"{attacker.DisplayLabel} 击溃");
                }

                if (attacker.Health <= 0f)
                {
                    RemoveUnit(attacker, "拒马反噬");
                }
            }
        }

        private SimUnit? ResolveTarget(SimUnit attacker)
        {
            if (attacker.AttackTargetId is not null)
            {
                var assigned = units.FirstOrDefault(u => u.Id == attacker.AttackTargetId && u.Alive);
                if (assigned is not null && !assigned.IsRouted)
                {
                    return assigned;
                }

                attacker.AttackTargetId = null;
            }

            // 斥候不主动接战（侦查专用）。
            if (attacker.IsScout)
            {
                return null;
            }

            // 剧本守势幕（CP-02 ①②）：**禁止自动接战**，仅在遭到攻击时反击（AttackTargetId 分支已处理）。
            // 设计依据：这两幕是"侦查 + 桶预养期"（TRUST-09），若弩兵按射程自动开火，
            // 两军会隔着丹水互射、斥候被瞬间清掉，情报循环（SLICE-03①）与七幕节拍同时失效。
            // 史实上这一阶段正是"秦数挑战，赵兵不出"——双方本就不交战。
            // 对**双方**生效：守势幕是"两军相持不战"（史实："秦数挑战，赵兵不出"）。
            if (CurrentAct <= CampaignAct.Challenge)
            {
                return null;
            }

            float reach = attacker.Definition.HasRangedAttack
                ? MathF.Max(attacker.Definition.AttackRange, attacker.Definition.RangedRange)
                : attacker.Definition.AttackRange;

            SimUnit? nearest = null;
            float best = float.MaxValue;
            foreach (var candidate in units.Where(u => u.Alive && u.Faction != attacker.Faction && !u.IsRouted))
            {
                float distance = attacker.Position.DistanceTo(candidate.Position);
                if (distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            if (nearest is not null && best <= reach)
            {
                attacker.AttackTargetId = nearest.Id;
                return nearest;
            }

            return null;
        }

        private void RemoveUnit(SimUnit unit, string cause)
        {
            if (unit.Removed)
            {
                return;
            }

            unit.Removed = true;
            unit.Health = MathF.Max(0f, unit.Health);
            unit.MoveGoal = null;
            unit.AttackTargetId = null;

            if (unit.Faction == Faction.Zhao)
            {
                stats.ZhaoLost++;
                stats.QinKills++;
            }
            else
            {
                stats.QinLost++;
                stats.ZhaoKills++;
            }

            Log(ChronicleKind.Combat, $"{unit.DisplayLabel} 离场（{cause}）");

            if (unit.IsCommander)
            {
                foreach (var ally in units.Where(u => u.Alive && u.Faction == unit.Faction))
                {
                    ally.Morale.OnCommanderKilled();
                }

                Log(ChronicleKind.Morale,
                    $"{unit.DisplayLabel} 阵亡：{FactionName(unit.Faction)}军全线士气 −30（COMBAT-10）");
            }
        }

        // ───────────────────────────── 移动 ─────────────────────────────

        private void TickMovement(float step)
        {
            foreach (var unit in units.Where(u => u.Alive))
            {
                MapPoint? goal = unit.IsRouted ? OwnBasePosition(unit.Faction) : unit.MoveGoal;
                if (goal is null)
                {
                    continue;
                }

                var terrain = Map.TerrainAt(unit.Position);
                float speed = unit.CurrentMoveSpeed(Rules, terrain);
                float travel = speed * step;
                float before = unit.Position.DistanceTo(goal.Value);
                // 边界与不可通行夹取（COMBAT-05/18）：不可进入绝壁、河道与壁垒非缺口段。
                unit.Position = Map.Clamp(unit.Position, unit.Position.MoveTowards(goal.Value, travel));

                if (unit.IsRouted && unit.Position.DistanceTo(goal.Value) <= 10f)
                {
                    unit.Removed = true;
                    Log(ChronicleKind.Morale, $"{unit.DisplayLabel} 溃逃回本阵，退出战场");
                }
                else if (!unit.IsRouted && before <= travel)
                {
                    unit.MoveGoal = null;
                }
            }
        }

        private MapPoint OwnBasePosition(Faction faction) => Map.BaseOf(faction);

        // ───────────────────────────── 火区 ─────────────────────────────

        private void TickFireZones(float step, float now)
        {
            foreach (var zone in fireZones.Where(z => z.IsActiveAt(now)).ToList())
            {
                int damage = zone.Settle(now);
                if (damage <= 0)
                {
                    continue;
                }

                var center = new MapPoint(zone.CenterX, zone.CenterZ);
                foreach (var unit in units.Where(u => u.Alive && u.Position.DistanceTo(center) <= zone.Radius).ToList())
                {
                    unit.Health -= damage;
                    if (unit.Health <= 0f)
                    {
                        RemoveUnit(unit, "火区灼烧");
                    }
                }

                Log(ChronicleKind.Combat, $"火区（{zone.CenterX:0},{zone.CenterZ:0}）灼烧 {damage} 点");
            }

            // 熄灭后移出（火区结算完毕，无残留）。
            fireZones.RemoveAll(z => !z.IsActiveAt(now));
        }

        // ───────────────────────────── 补给与饥饿 ─────────────────────────────

        private void TickSupply(float step)
        {
            TickSupplyPath(step);

            supplyAccumulator += step;
            if (supplyAccumulator < Rules.Supply.RationTickSeconds)
            {
                return;
            }

            supplyAccumulator -= Rules.Supply.RationTickSeconds;

            foreach (var unit in units.Where(u => u.Alive).ToList())
            {
                // 己方粮仓补给（FOOD-03）：未焚毁、距离足够近、**且补给路径连通** → 补满携粮。
                // FOOD-06：路径被切断（合围）时即使紧邻粮仓也补不上——粮车进不来。
                bool pathOpen = IsZhaoPathOpenCached && unit.Faction == Faction.Zhao
                    || unit.Faction != Faction.Zhao && IsSupplyPathOpen(unit.Faction);
                bool nearGranary = pathOpen && Map.GranariesOf(unit.Faction).Any(g =>
                    !g.IsBurned && unit.Position.DistanceTo(g.Position) <= Rules.GranaryResupplyRadius);

                // FOOD-06：补给路径恢复后饥饿度按每 tick −5% 回落（不清零），
                // 即便部队尚未走到粮仓半径内——粮道通了就吃得上饭。
                if (pathOpen && unit.Starvation > 0f)
                {
                    unit.RecoverStarvation(Rules.Supply.StarvationRecoveryPerTick);
                }

                if (nearGranary)
                {
                    unit.Resupply();
                    continue;
                }

                if (!unit.ConsumeRationTick(1f))
                {
                    // 仍有存粮：只是欠补，不入饥饿路径。
                    continue;
                }

                // FOOD-06：补给路径连通时**不进入饥饿累积**——粮道通了就吃得上饭，
                // 只是尚未走到粮仓半径内（前面已按 −5%/tick 回落）。
                if (pathOpen)
                {
                    continue;
                }

                // FOOD-06③：被围困（路径切断）持续 ≥3 分钟后，饥饿累积 ×1.5。
                float siegeMultiplier = siegeSeconds < Rules.Supply.SiegeMultiplierAfterSeconds
                    ? 1f
                    : Rules.Supply.SiegeStarvationMultiplier;

                unit.ApplyStarvationTick(Rules.Supply.StarvationGainPerTick * siegeMultiplier);

                if (unit.Starvation > Rules.Supply.MoraleDamageThreshold)
                {
                    unit.Morale.OnStarvationTick();
                }

                if (unit.Starvation >= Rules.StarvationDisbandThreshold)
                {
                    unit.Removed = true;
                    Log(ChronicleKind.Supply, $"{unit.DisplayLabel} 粮尽饿散，退出战场（FOOD-04）");
                }
                else
                {
                    Log(ChronicleKind.Supply,
                        $"{unit.DisplayLabel} 粮尽，饥饿度 {unit.Starvation * 100f:0}%（攻 −20%、移速 −30%，FOOD-04）" +
                        (siegeMultiplier > 1f ? "【被围困：饥饿累积 ×1.5】" : string.Empty));
                }
            }
        }

        /// <summary>
        /// FOOD-06 补给路径判定：按 5 战场秒的节拍判断"己方粮仓 → 部队"的补给线是否连通。
        ///
        /// 简化口径（真源 FOOD-06）：断粮条件为**渡口被敌方占据**或**粮道走廊被敌方阻隔**；
        /// 粮仓全焚亦视为断粮。判定节拍与围困时长分开累计，围困 ≥3 分钟后饥饿 ×1.5。
        /// </summary>
        private void TickSupplyPath(float step)
        {
            supplyPathAccumulator += step;
            if (supplyPathAccumulator < Rules.Supply.SupplyPathCheckIntervalSeconds)
            {
                return;
            }

            supplyPathAccumulator -= Rules.Supply.SupplyPathCheckIntervalSeconds;

            bool zhaoOpen = IsSupplyPathOpen(Faction.Zhao);
            zhaoPathOpen = zhaoOpen;

            if (zhaoOpen)
            {
                bool wasSieged = siegeSeconds > 0f;
                siegeSeconds = 0f;
                siegeAmplified = false;

                if (wasSieged && siegeAnnounced)
                {
                    siegeAnnounced = false;
                    Log(ChronicleKind.Supply, "赵军补给路径恢复——饥饿累积回到正常速率（FOOD-06）");
                }

                return;
            }

            siegeSeconds += Rules.Supply.SupplyPathCheckIntervalSeconds;

            if (!siegeAnnounced)
            {
                siegeAnnounced = true;
                Log(ChronicleKind.Supply,
                    "赵军补给路径被切断——粮道已绝，饥饿开始累积（FOOD-06；>3 分钟后累积 ×1.5）");
            }

            if (siegeSeconds >= Rules.Supply.SiegeMultiplierAfterSeconds && !siegeAmplified)
            {
                siegeAmplified = true;
                Log(ChronicleKind.Supply, "围困已逾 3 分钟——饥饿累积 ×1.5（FOOD-06③）");
            }
        }

        /// <summary>赵方补给路径的**缓存**状态（由 <see cref="TickSupplyPath"/> 按判定节拍刷新）。</summary>
        private bool IsZhaoPathOpenCached => zhaoPathOpen;

        /// <summary>补给路径是否连通（FOOD-06）：粮仓未全焚，且渡口与粮道走廊无敌方占据。</summary>
        public bool IsSupplyPathOpen(Faction faction)
        {
            if (!Map.GranariesOf(faction).Any(g => !g.IsBurned))
            {
                return false;
            }

            var ford = FordOf();
            if (ford is null)
            {
                return true; // 无渡口的地图不做封锁判定（教学序章图）。
            }

            bool enemyHoldsFord = units.Any(u => u.Alive && u.Faction != faction && !u.IsScout &&
                                                 u.Position.DistanceTo(ford.Center) <= Rules.GranaryResupplyRadius);
            if (enemyHoldsFord)
            {
                return false;
            }

            // 粮道走廊（渡口所在区域）被敌方成建制占据（≥2 队）亦视为切断。
            int enemyInCorridor = units.Count(u => u.Alive && u.Faction != faction && !u.IsScout &&
                                                   Map.AreaIdAt(u.Position) == Map.AreaIdAt(ford.Center));
            return enemyInCorridor < 2;
        }

        /// <summary>丹水渡口（嵌在不可通行河道内的可通行小体块，MAP-11）。</summary>
        private TerrainVolume? FordOf() =>
            Map.Volumes
                .Where(v => v.Terrain == TerrainClass.Passable)
                .Where(v => Map.Volumes.Any(big => big.Terrain == TerrainClass.Impassable &&
                                                   System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                   System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .OrderBy(v => v.Width * v.Depth)
                .FirstOrDefault();

        // ───────────────────────────── 侦查与情报 ─────────────────────────────

        private void TickObservation(float step, float now)
        {
            observationAccumulator += step;
            if (observationAccumulator < Rules.ObservationIntervalSeconds)
            {
                return;
            }

            observationAccumulator -= Rules.ObservationIntervalSeconds;

            Observe(Faction.Zhao, now);
            Observe(Faction.Qin, now);
        }

        private void Observe(Faction observer, float now)
        {
            var scouts = units.Where(u => u.Alive && u.Faction == observer && u.IsScout).ToList();
            var enemies = units.Where(u => u.Alive && u.Faction != observer).ToList();
            if (scouts.Count == 0 || enemies.Count == 0)
            {
                return;
            }

            foreach (var scout in scouts)
            {
                var visible = enemies.Where(e => scout.Position.DistanceTo(e.Position) <= Rules.ScoutSightRadius).ToList();
                if (visible.Count == 0)
                {
                    continue;
                }

                scout.ObservationMinutes += Rules.ObservationIntervalSeconds / 60f;

                var centroid = new MapPoint(visible.Average(u => u.Position.X), visible.Average(u => u.Position.Z));
                float trueMen = visible.Count * SimUnit.MenPerUnit;

                // 欺骗痕迹篡改（DECP-01/02/03）：观察区域存在【敌方】活跃痕迹 → 远观采样吃篡改系数。
                var trace = ActiveTraceAgainst(observer, Map.AreaIdAt(centroid), now);
                float observedMen = trace is null ? trueMen : trueMen * trace.Definition.TroopEstimateMultiplier;

                // 识破路径①（DECP-04）：斥候进入假情报生成点 50 米内实体清点 → 看到真值并识破。
                bool proximity = DeceptionDetector.DetectByProximity(scout.Position.DistanceTo(centroid), Rules.Detection);
                bool detected = proximity && trace is not null;
                if (detected)
                {
                    observedMen = trueMen;
                }

                float credibility = CredibilityCalculator.Fresh(
                    Rules.Credibility, IntelSourceType.ScoutVisual, scouts.Count, scout.ObservationMinutes);

                // 主题 = 被观察的【敌】方兵力（接收方情报池里存的是对敌方的情报，TRUST-01 分桶键）。
                string topic = TopicOf(OpponentOf(observer));

                var record = new IntelRecord
                {
                    IntelId = $"INTEL_{observer}_{++intelSequence:D4}",
                    Topic = topic,
                    SourceType = IntelSourceType.ScoutVisual,
                    BaseCredibility = Rules.Credibility.ScoutVisualBase,
                    RawContent = new IntelPayload(IntelPayloadType.TroopCountEstimate, observedMen),
                    ScoutCount = scouts.Count,
                    ObservationMinutes = scout.ObservationMinutes,
                    CreatedAtSeconds = now,
                };
                record.Initialize(credibility);
                PoolOf(observer).Add(record);

                AuthoritativeRecords.Add(new AuthoritativeIntel
                {
                    IntelId = record.IntelId,
                    TrueContent = new IntelPayload(IntelPayloadType.TroopCountEstimate, trueMen),
                    IsFabricated = trace is not null,
                    FabricatedBy = trace is null ? null : FactionName(OpponentOf(observer)),
                    SkillId = trace?.Definition.Id.ToString(),
                    ContentMultiplier = trace?.Definition.TroopEstimateMultiplier ?? 1f,
                    Detected = detected,
                });

                Log(ChronicleKind.Intel,
                    $"{FactionName(observer)}军斥候回报：{FactionName(OpponentOf(observer))}军兵力约 {observedMen:0} 人" +
                    $"（可信度 {credibility:0}%，{TierName(CredibilityCalculator.Tier(credibility, Rules.Credibility))}）" +
                    (detected ? "【近距清点：识破伪造】" : string.Empty));

                if (observer == Faction.Qin)
                {
                    // AI 侧采信链路：信任桶双向更新 + 联合门控（TRUST-02/05，AI-01）。
                    // 桶按"被观察方主题"分桶——AI 采信的是关于赵军的情报（TRUST-01）。
                    float? aggregate = PoolOf(observer).AggregateCredibility(topic, now);
                    int distinctKinds = PoolOf(observer).DistinctSourceKinds(topic);
                    var (trustValue, gate) = AiBrain.Decision.OnIntel(
                        topic, credibility, distinctKinds, aggregate ?? credibility, now);
                    lastPayloadAiSaw = observedMen;
                    Log(ChronicleKind.AiDecision,
                        $"秦军采信链路：{topic} 桶 {trustValue:0.00} → 门控 {gate}（载荷 {observedMen:0} 人）");
                }

                if (detected)
                {
                    OnDeceptionDetected(observer, trace!.Topic, now);
                }
            }
        }

        /// <summary>
        /// 识破处置（TRUST-06/DECP-06/SKILL-11）：<paramref name="detector"/> 已在近距清点中看到真值——
        /// ①受害方该主题在途伪造条目标伪（可信度 15%、退出综合计算）；
        /// ②欺骗方 AI 信任桶置 0.20 并进入识破型【弱】复核（TRUST-09；玩家无信任桶，AI-05）；
        /// ③识破方谋略点 +15（受 100 上限约束，SKILL-11 回收渠道）。
        /// </summary>
        private void OnDeceptionDetected(Faction detector, string topic, float now)
        {
            if (detector == Faction.Qin)
            {
                // AI 识破玩家：AI 侧在途伪造条目标伪、玩家无桶可置、AI +15 谋略点、进入弱复核。
                foreach (var record in AiIntel.Records.Where(r => r.Topic == topic && r.Status == IntelStatus.Active).ToList())
                {
                    record.FlagAsFake();
                }

                AiBrain.Decision.MarkDeceptionDetected(topic, now);
                AiPoints.AwardDeceptionDetected();
                stats.AiDetections++;
                Log(ChronicleKind.Detection,
                    $"秦军近距清点识破赵军欺诈：伪造条目标伪、秦军进入识破型弱复核（TRUST-09）、秦军谋略点 +15");
                return;
            }

            // 玩家识破 AI：玩家在途伪造条目标伪 → 情报栏显示"？"印章（UI-03）；AI 桶置 0.20；玩家 +15 谋略点。
            foreach (var record in PlayerIntel.Records.Where(r => r.Topic == topic && r.Status == IntelStatus.Active).ToList())
            {
                record.FlagAsFake();
            }

            AiTrust.SetDetected(topic, now);
            AiBrain.Decision.MarkDeceptionDetected(topic, now);
            PlayerPoints.AwardDeceptionDetected();
            stats.PlayerDetections++;
            Log(ChronicleKind.Detection,
                $"赵军近距清点识破秦军欺诈：情报标伪（15%）、秦军该主题信任桶置 0.20、赵军谋略点 +15（SKILL-11）");
        }

        private DeceptionTrace? ActiveTraceAgainst(Faction observer, string areaId, float now) =>
            deceptionTraces
                .Where(t => t.Owner != observer && t.Trace.AreaId == areaId && t.Trace.IsActiveAt(now))
                .Select(t => t.Trace)
                .FirstOrDefault();

        // ───────────────────────────── 技能 ─────────────────────────────

        public bool CanCast(Faction caster, StratagemId id, out string reason)
        {
            var definition = DefinitionOf(id);
            if (definition is null)
            {
                reason = "该技能不在切片技能池（SKILL-10）";
                return false;
            }

            var wallet = caster == Faction.Zhao ? PlayerPoints : AiPoints;
            if (!wallet.CanAfford(definition.StrategyPointCost))
            {
                reason = $"谋略点不足（需 {definition.StrategyPointCost}，余 {wallet.Points}）";
                return false;
            }

            var lastCast = caster == Faction.Zhao ? playerLastCast : aiLastCast;
            if (lastCast.TryGetValue(id, out float last) &&
                Clock.ElapsedSeconds - last < definition.BattleCooldownSeconds)
            {
                reason = $"冷却中（还需 {definition.BattleCooldownSeconds - (Clock.ElapsedSeconds - last):0} 秒）";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public float CooldownRemaining(Faction caster, StratagemId id)
        {
            var definition = DefinitionOf(id);
            var lastCast = caster == Faction.Zhao ? playerLastCast : aiLastCast;
            if (definition is null || !lastCast.TryGetValue(id, out float last))
            {
                return 0f;
            }

            return MathF.Max(0f, definition.BattleCooldownSeconds - (Clock.ElapsedSeconds - last));
        }

        /// <summary>
        /// 施放技能（玩家/AI 同规则，AI-05）。减灶/增灶 → 在目标区域挂欺骗痕迹（篡改后续远观采样）；
        /// 火攻 → 销毁目标囤积点 40% 粮草 + 60 战场秒火区 + 浓烟 skill_reveal 情报（SKILL-03）。
        /// </summary>
        public bool TryCast(Faction caster, StratagemId id, MapPoint aim)
        {
            if (!CanCast(caster, id, out string reason))
            {
                Log(ChronicleKind.Cast, $"{FactionName(caster)}军施计失败：{reason}");
                return false;
            }

            var definition = DefinitionOf(id)!;
            (caster == Faction.Zhao ? PlayerPoints : AiPoints).TrySpend(definition.StrategyPointCost);
            (caster == Faction.Zhao ? playerLastCast : aiLastCast)[id] = Clock.ElapsedSeconds;
            if (caster == Faction.Zhao)
            {
                stats.PlayerCasts++;
            }
            else
            {
                stats.AiCasts++;
            }

            switch (id)
            {
                case StratagemId.ReduceStove:
                case StratagemId.AddStove:
                    deceptionTraces.Add((caster, new DeceptionTrace(
                        definition, Clock.ElapsedSeconds, Map.AreaIdAt(aim), TopicOf(caster))));
                    Log(ChronicleKind.Cast,
                        $"{FactionName(caster)}军施放{definition.DisplayName}于{AreaName(Map.AreaIdAt(aim))}：" +
                        $"施计窗口 {definition.CastWindowSeconds:0} 秒 + 痕迹存续 {definition.TraceDurationSeconds / 60f:0} 分钟，" +
                        $"远观采样 ×{definition.TroopEstimateMultiplier:0.0}（DECP-02/03）");
                    return true;

                case StratagemId.FireAttack:
                    return CastFireAttack(caster, aim);

                default:
                    return false;
            }
        }

        private bool CastFireAttack(Faction caster, MapPoint aim)
        {
            var target = Map.GranariesOf(OpponentOf(caster))
                .Where(g => !g.IsBurned)
                .OrderBy(g => g.Position.DistanceTo(aim))
                .FirstOrDefault();

            if (target is null)
            {
                Log(ChronicleKind.Cast, $"{FactionName(caster)}军火攻失败：敌方已无可用囤积点");
                return false;
            }

            float destroyed = target.Stock * (Rules.Supply.FireAttackRationLossPercent / 100f);
            target.Stock -= destroyed;

            fireZones.Add(new FireZone(Clock.ElapsedSeconds, Rules.Combat)
            {
                CenterX = target.Position.X,
                CenterZ = target.Position.Z,
            });

            Log(ChronicleKind.Cast,
                $"{FactionName(caster)}军火攻{target.DisplayName}：焚毁 {destroyed:0} 单位粮草（{Rules.Supply.FireAttackRationLossPercent}%），" +
                $"形成 {Rules.Combat.FireZoneDurationSeconds:0} 秒火区（灼烧 {Rules.Combat.FireZoneBurnDamagePerTick} 点/{Rules.Combat.FireZoneBurnIntervalSeconds:0} 秒，SKILL-03）");

            // 浓烟 skill_reveal：向所有有视野的一方各生成一条真实情报（基值 75%、不可伪造，SLICE-03⑤）。
            foreach (var faction in new[] { Faction.Zhao, Faction.Qin })
            {
                bool hasVision = units.Any(u => u.Alive && u.Faction == faction &&
                                                u.Position.DistanceTo(target.Position) <= Rules.ScoutSightRadius);
                if (!hasVision)
                {
                    continue;
                }

                var record = FireRevealFactory.Create(
                    TopicOf(OpponentOf(faction)), target.Position.X, target.Position.Z,
                    Clock.ElapsedSeconds, faction.ToString());
                PoolOf(faction).Add(record);
                Log(ChronicleKind.Intel,
                    $"{FactionName(faction)}军目睹浓烟：敌方在此使用火攻（skill_reveal 基值 75%、不可伪造、不可拦截）");
            }

            return true;
        }

        private static StratagemDefinition? DefinitionOf(StratagemId id) => id switch
        {
            StratagemId.ReduceStove => StratagemDefinition.ReduceStove,
            StratagemId.AddStove => StratagemDefinition.AddStove,
            StratagemId.FireAttack => StratagemDefinition.FireAttack,
            _ => null, // 其余九计为二期草案（SKILL-13），切片不实现
        };

        // ───────────────────────────── 玩家指令 ─────────────────────────────

        public void OrderMove(string unitId, MapPoint goal)
        {
            var unit = units.FirstOrDefault(u => u.Id == unitId && u.Alive);
            if (unit is null)
            {
                return;
            }

            unit.MoveGoal = Map.ClampToBounds(goal);
            unit.AttackTargetId = null;
        }

        public void OrderAttack(string unitId, string targetId)
        {
            var unit = units.FirstOrDefault(u => u.Id == unitId && u.Alive);
            var target = units.FirstOrDefault(u => u.Id == targetId && u.Alive);
            if (unit is null || target is null)
            {
                return;
            }

            unit.AttackTargetId = target.Id;
            unit.MoveGoal = target.Position;
        }

        public void OrderStop(string unitId)
        {
            var unit = units.FirstOrDefault(u => u.Id == unitId && u.Alive);
            if (unit is null)
            {
                return;
            }

            unit.MoveGoal = null;
            unit.AttackTargetId = null;
        }

        /// <summary>切换结阵姿态（COMBAT-17，仅枪兵可结阵；默认开）。</summary>
        public void ToggleFormation(string unitId)
        {
            var unit = units.FirstOrDefault(u => u.Id == unitId && u.Alive);
            if (unit is null || !unit.Definition.SpearFormationCapable)
            {
                return;
            }

            unit.Formed = !unit.Formed;
            Log(ChronicleKind.Combat,
                $"{unit.DisplayLabel} {(unit.Formed ? "结阵" : "解阵")}（结阵：移速 −20%、免疫冲锋击退、拒马反噬 6 点/发）");
        }

        // ───────────────────────────── 据点与胜负 ─────────────────────────────

        private void TickCapture(float step)
        {
            foreach (var prop in Map.Strongholds().ToList())
            {
                bool contested = units.Any(u => u.Alive && u.Faction != prop.Owner && !u.IsRouted &&
                                                u.Position.DistanceTo(prop.Position) <= prop.Radius);
                if (!contested)
                {
                    prop.CaptureProgress = 0f;
                    continue;
                }

                prop.CaptureProgress += step;
                if (prop.CaptureProgress >= Rules.CaptureHoldSeconds)
                {
                    bool zhaoWins = prop.Owner == Faction.Qin;
                    Finish(zhaoWins ? BattleOutcome.ZhaoVictory : BattleOutcome.QinVictory,
                        $"占领{prop.DisplayName}（连续驻留 {Rules.CaptureHoldSeconds:0} 秒，GDD §2.7.1 夺取关键据点）");
                }
            }
        }

        private void EvaluateOutcome()
        {
            if (IsFinished)
            {
                return;
            }

            bool zhaoAlive = units.Any(u => u.Alive && u.Faction == Faction.Zhao);
            bool qinAlive = units.Any(u => u.Alive && u.Faction == Faction.Qin);

            if (!qinAlive)
            {
                Finish(BattleOutcome.ZhaoVictory, "歼灭秦军主力（GDD §2.7.1）");
                return;
            }

            if (!zhaoAlive)
            {
                Finish(BattleOutcome.QinVictory, "赵军主力尽墨（GDD §2.7.1）");
                return;
            }

            if (Map.GranariesOf(Faction.Qin).All(g => g.IsBurned))
            {
                Finish(BattleOutcome.ZhaoVictory, "焚毁秦军全部粮道（GDD §2.7.1）");
                return;
            }

            if (Map.GranariesOf(Faction.Zhao).All(g => g.IsBurned))
            {
                Finish(BattleOutcome.QinVictory, "赵军粮道尽焚（GDD §2.7.1）");
                return;
            }

            if (Clock.ElapsedSeconds < Rules.TimeLimitSeconds)
            {
                return;
            }

            if (stats.ZhaoKills == stats.QinKills)
            {
                Finish(BattleOutcome.Draw, $"20 分钟硬上限：歼敌数持平（赵 {stats.ZhaoKills} : 秦 {stats.QinKills}）");
            }
            else
            {
                bool zhaoWins = stats.ZhaoKills > stats.QinKills;
                Finish(zhaoWins ? BattleOutcome.ZhaoVictory : BattleOutcome.QinVictory,
                    $"20 分钟硬上限：按歼敌数判定（赵 {stats.ZhaoKills} : 秦 {stats.QinKills}，GDD §2.7.1 时限判定）");
            }
        }

        private void Finish(BattleOutcome outcome, string reason)
        {
            Outcome = outcome;
            OutcomeReason = reason;
            Log(ChronicleKind.Outcome, $"战役结束：{OutcomeName(outcome)}——{reason}");
        }

        // ───────────────────────────── 查询与工具 ─────────────────────────────

        private IntelPool PoolOf(Faction faction) => faction == Faction.Zhao ? PlayerIntel : AiIntel;

        private string TopicOf(Faction owner) => owner == Faction.Zhao ? "赵军_兵力" : "秦军_兵力";

        private static Faction OpponentOf(Faction faction) => faction == Faction.Zhao ? Faction.Qin : Faction.Zhao;

        public static string FactionName(Faction faction) => faction == Faction.Zhao ? "赵" : "秦";

        private string AreaName(string areaId) => Map.AreaDisplayName(areaId);

        private static string TierName(CredibilityTier tier) => tier switch
        {
            CredibilityTier.Confident => "确信",
            CredibilityTier.Trusted => "可信",
            CredibilityTier.Doubtful => "存疑",
            _ => "不可信",
        };

        private static string OutcomeName(BattleOutcome outcome) => outcome switch
        {
            BattleOutcome.ZhaoVictory => "赵军胜",
            BattleOutcome.QinVictory => "秦军胜",
            BattleOutcome.Draw => "平局",
            _ => "未决",
        };

        /// <summary>兵力评估（AI-05 兵力对比口径）：存活单位剩余血量折算的人数。</summary>
        public float EffectiveStrength(Faction faction) =>
            units.Where(u => u.Alive && u.Faction == faction).Sum(u => u.HealthRatio * SimUnit.MenPerUnit);

        /// <summary>AI 最近一次采信到的载荷（-1 = 尚无情报）。</summary>
        public float LastPayloadAiSaw => lastPayloadAiSaw;

        public bool AiIsFrozen(float now) => now < aiFreezeUntilSeconds;

        /// <summary>TRUST-07：存疑型【强】复核——冻结全部高利害行动（不进攻/不撤退/不调兵）。</summary>
        public void EnterAiStrongRecheck(float now)
        {
            aiFreezeUntilSeconds = now + Rules.StrongRecheckSeconds;
            foreach (var unit in units.Where(u => u.Alive && u.Faction == Faction.Qin))
            {
                unit.MoveGoal = null;
                unit.AttackTargetId = null;
            }

            Log(ChronicleKind.AiDecision,
                $"秦军触发存疑型【强】复核：部署斥候、冻结高利害行动 {Rules.StrongRecheckSeconds:0} 秒（TRUST-07）");
        }

        /// <summary>玩家侧情报栏快照（SLICE-03④；UI-02/03 展示口径）。</summary>
        public IReadOnlyList<IntelFeedItem> PlayerIntelFeed(float now)
        {
            PlayerIntel.Tick(now);
            return PlayerIntel.Records
                .OrderByDescending(r => r.CreatedAtSeconds)
                .Select(r =>
                {
                    float credibility = r.Tick(now, Rules.Credibility);
                    return new IntelFeedItem
                    {
                        IntelId = r.IntelId,
                        Topic = r.Topic,
                        SourceType = r.SourceType,
                        Value = r.RawContent.Value,
                        Credibility = credibility,
                        Tier = CredibilityCalculator.Tier(credibility, Rules.Credibility),
                        CreatedAtSeconds = r.CreatedAtSeconds,
                        Status = r.Status,
                    };
                })
                .ToList();
        }

        internal void LogAi(string text) => Log(ChronicleKind.AiDecision, text);

        /// <summary>
        /// 登记 AI 欺骗施计（AI-05：与玩家同规则，走同一痕迹/识破链路）。
        /// 由 <see cref="AiBrain"/> 在满足触发条件时调用。
        /// </summary>
        public void RegisterAiDeception(AiDeceptionCast cast)
        {
            deceptionTraces.Add((Faction.Qin, cast.Trace));
            aiLastCast[cast.Definition.Id] = Clock.ElapsedSeconds;
            stats.AiCasts++;
            Log(ChronicleKind.Cast,
                $"秦军施放{cast.Definition.DisplayName}于{AreaName(cast.Trace.AreaId)}（AI-05）：真实兵力 {cast.TrueTroopCount:0} 人、" +
                $"伪造载荷 {cast.FabricatedTroopCount:0} 人（×{cast.Definition.TroopEstimateMultiplier:0.0}）");
        }

        private void Log(ChronicleKind kind, string text) =>
            chronicle.Add(new ChronicleEntry(Clock.ElapsedSeconds, kind, text));
    }

}