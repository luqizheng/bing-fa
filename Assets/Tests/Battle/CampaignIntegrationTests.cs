using System.Linq;
using ChinaBettle.Battle;
using ChinaBettle.Foundation.Intel;
using ChinaBettle.Foundation.Map;
using ChinaBettle.Foundation.Units;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 剧本 ↔ 仿真集成回归（真源 CP-01…08、ADV-07、SKILL-13）。
    ///
    /// 单元测试（<see cref="ChangpingCampaignTests"/>）锁定剧本自身逻辑；
    /// 本文件锁定**接入行为**：幕在战役时钟推进下真的会走、换将载荷真的进了情报池、
    /// 合围态势真的改变了秦军目标、结算顺序真的是"战术优先于剧本"。
    /// </summary>
    public sealed class CampaignIntegrationTests
    {
        private static BattleSimulation NewSim(bool autoPlayAi = false) =>
            new(new BattleRules(), autoPlayAi, SliceMaps.ChangpingV1);

        [Test]
        public void Battle_StartsInStandoffAct()
        {
            var sim = NewSim();

            Assert.That(sim.CurrentAct, Is.EqualTo(CampaignAct.Standoff), "CP-02：开局即进入第 ① 幕");
            Assert.That(sim.CurrentActTitle, Does.Contain("对峙"));
            Assert.That(sim.Chronicle.Any(c => c.Kind == ChronicleKind.Campaign), Is.True,
                "幕进入应写入编年史（GDD §5.4）");
        }

        [Test]
        public void CampaignAdvances_AsBattleClockRuns()
        {
            var sim = NewSim();

            // 推进过①对峙+②挑战（阈值之和）后应完成换将并抵达出垒幕。
            sim.Tick(ChangpingCampaign.StandoffSeconds + ChangpingCampaign.ChallengeSeconds + 5f);

            Assert.That(sim.CurrentAct, Is.GreaterThanOrEqualTo(CampaignAct.Sortie),
                "时钟推进后剧本应已跨过换将幕（CP-02/03）");
            Assert.That(sim.Campaign.History.Select(h => h.Act), Does.Contain(CampaignAct.Dismissal),
                "幕历史应含换将幕（复盘要求，GDD §5.4）");
        }

        [Test]
        public void Dismissal_InjectsNarrativeIntel_IntoPlayerPool()
        {
            var sim = NewSim();
            int before = sim.PlayerIntel.Records.Count;

            sim.Tick(ChangpingCampaign.StandoffSeconds + ChangpingCampaign.ChallengeSeconds + 5f);

            Assert.That(sim.PlayerIntel.Records.Count, Is.GreaterThan(before),
                "换将叙事载荷应进入玩家情报池（情报规格 §3）");

            var narrative = sim.PlayerIntel.Records.First(r => r.IntelId.StartsWith("INTEL_NARRATIVE"));
            Assert.That(narrative.SourceType, Is.Not.EqualTo(IntelSourceType.SkillReveal),
                "SKILL-13：换将载荷是叙事事件，不是技能暴露");
            Assert.That(narrative.Status, Is.EqualTo(IntelStatus.Active),
                "叙事载荷不可被识破（无 DECP-01 伪造语义），保持 Active");
        }

        [Test]
        public void Dismissal_LeavesNoDeceptionTrace()
        {
            var sim = NewSim();

            sim.Tick(ChangpingCampaign.StandoffSeconds + ChangpingCampaign.ChallengeSeconds + 5f);

            // 换将不产生欺骗痕迹 → 玩家识破路径不应因它而触发（SKILL-13）。
            Assert.That(sim.Stats.PlayerDetections, Is.Zero,
                "叙事载荷不是伪造情报，不应产生识破");
            Assert.That(sim.Units.Any(u => u.IsScout), Is.True, "斥候仍在场（剧本不夺兵）");
        }

        [Test]
        public void Campaign_ReachesEncirclement_AndRedirectsQinToTheFord()
        {
            var sim = NewSim(autoPlayAi: true);

            // 推进到**合围幕出现**为止（幕阈值会把幕推过，故不做"恰好停在某幕"的脆弱断言）。
            float elapsed = 0f;
            while (sim.CurrentAct < CampaignAct.Encirclement && !sim.IsFinished && elapsed < 900f)
            {
                sim.Tick(5f);
                elapsed += 5f;
            }

            Assert.That(sim.Campaign.History.Select(h => h.Act), Does.Contain(CampaignAct.Encirclement),
                "出垒后应进入合围幕（CP-06③；幕可能已继续推进，故查幕历史）");

            // 合围态势应把秦军调向丹水渡口（封锁粮道，CP-06）。
            var encl = sim.Campaign.History.First(h => h.Act == CampaignAct.Encirclement);
            Assert.That(encl.Title, Does.Contain("合围"), "幕标题可读（HUD/复盘用）");

            // 合围态势是**持续**的：围困幕不得把它撤销（否则秦军回守势线、包围圈散开）。
            Assert.That(sim.AiPosture, Is.EqualTo(AiPosture.Encircle),
                "围困期应保持合围态势（CP-06 就地封锁，不撤围）");

            sim.Tick(120f); // 让秦军开到封锁位置（抵达后 MoveGoal 会被清空，故断言位置而非目标）
            var ford = FindFord(sim);
            Assert.That(ford, Is.Not.Null, "长平图应有丹水渡口（MAP-11）");

            var qinCombat = sim.Units.Where(u => u.Alive && u.Faction == Faction.Qin && !u.IsScout).ToList();
            Assert.That(qinCombat, Is.Not.Empty, "围困期秦军应仍有封锁部队");
            Assert.That(qinCombat.Any(u => u.Position.DistanceTo(ford!.Center) <= 80f), Is.True,
                $"合围态势应把秦军调到丹水渡口封锁粮道（CP-06）；实际位置：{string.Join(" / ", qinCombat.Select(u => u.Position.ToString()))}");
        }

        /// <summary>渡口＝嵌在不可通行河道内的可通行小体块（按地图语义取，不硬编码坐标）。</summary>
        private static TerrainVolume? FindFord(BattleSimulation sim) =>
            sim.Map.Volumes
                .Where(v => v.Terrain == TerrainClass.Passable)
                .Where(v => sim.Map.Volumes.Any(big => big.Terrain == TerrainClass.Impassable &&
                                                        System.MathF.Abs(v.Center.X - big.Center.X) <= big.Width * 0.5f &&
                                                        System.MathF.Abs(v.Center.Z - big.Center.Z) <= big.Depth * 0.5f))
                .OrderBy(v => v.Width * v.Depth)
                .FirstOrDefault();

        [Test]
        public void TacticalOutcome_TakesPrecedenceOverCampaignEnding()
        {
            var sim = NewSim();

            // 全歼秦军：应结算为"歼灭"战术结果，而非剧本的态势性结局（CP-08）。
            foreach (var unit in sim.Units.Where(u => u.Faction == Faction.Qin).ToList())
            {
                unit.Removed = true;
            }

            sim.Tick(0.2f);

            Assert.That(sim.IsFinished, Is.True);
            Assert.That(sim.OutcomeReason, Does.Contain("歼灭"), "战术结算优先于剧本结局（结算顺序契约）");
        }

        [Test]
        public void PausedClock_DoesNotAdvanceCampaign()
        {
            var sim = NewSim();
            sim.Clock.Pause();
            var actBefore = sim.CurrentAct;
            int historyBefore = sim.Campaign.History.Count;

            sim.Tick(300f);

            Assert.That(sim.CurrentAct, Is.EqualTo(actBefore), "TIME-02：暂停冻结剧本推进");
            Assert.That(sim.Campaign.History.Count, Is.EqualTo(historyBefore));
        }
    }

}
