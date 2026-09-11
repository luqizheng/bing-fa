using System.Linq;
using ChinaBettle.Battle.Campaign;
using NUnit.Framework;

namespace ChinaBettle.Tests.Battle
{
    /// <summary>
    /// 存档序列化回归（GDD §5.4：JSON 存本地/Steam Cloud）。
    ///
    /// 锁定性质：往返一致（写→读→写 内容相同）、转义正确（中文/引号/换行不破坏 JSON）、
    /// 损坏输入降级不崩溃、版本号可读（供日后迁移）。
    /// </summary>
    public sealed class BattleSaveSerializerTests
    {
        private static BattleSave Sample() => new BattleSave
        {
            MapId = "changping_v1",
            ElapsedSeconds = 195.5f,
            CurrentAct = "Encirclement",
            Finished = true,
            OutcomeReason = "史实结局·长平之败（CP-08）",
            PlayerPoints = 70,
            Units =
            {
                new UnitSave { Id = "zhao_00", Label = "赵骑1(主将)", Faction = "Zhao", DefinitionId = "zhao_hufu",
                               X = -60f, Z = -140f, Health = 100f, RationsUnits = 18f, Starvation = 0f, Formed = false },
                new UnitSave { Id = "zhao_02", Label = "赵枪1", Faction = "Zhao", DefinitionId = "spear",
                               X = -30f, Z = -90f, Health = 84f, RationsUnits = 12f, Starvation = 0.2f, Formed = true },
            },
            Chronicle =
            {
                new ChronicleSave { AtSeconds = 0f, Kind = "Campaign", Text = "① 开局对峙｜赵军据西垒" },
                new ChronicleSave { AtSeconds = 150f, Kind = "Intel", Text = "秦军斥候回报：赵军兵力约 900 人（可信度 69%）" },
            },
        };

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = Sample();
            var restored = BattleSaveSerializer.Deserialize(BattleSaveSerializer.Serialize(original));

            Assert.That(restored.MapId, Is.EqualTo("changping_v1"));
            Assert.That(restored.ElapsedSeconds, Is.EqualTo(195.5f).Within(1e-3f));
            Assert.That(restored.CurrentAct, Is.EqualTo("Encirclement"));
            Assert.That(restored.Finished, Is.True);
            Assert.That(restored.OutcomeReason, Is.EqualTo("史实结局·长平之败（CP-08）"));
            Assert.That(restored.PlayerPoints, Is.EqualTo(70));
            Assert.That(restored.Units, Has.Count.EqualTo(2));
            Assert.That(restored.Chronicle, Has.Count.EqualTo(2));
        }

        [Test]
        public void RoundTrip_PreservesUnitDetails()
        {
            var restored = BattleSaveSerializer.Deserialize(BattleSaveSerializer.Serialize(Sample()));

            var spear = restored.Units.Single(u => u.Id == "zhao_02");
            Assert.That(spear.Label, Is.EqualTo("赵枪1"));
            Assert.That(spear.DefinitionId, Is.EqualTo("spear"));
            Assert.That(spear.Health, Is.EqualTo(84f).Within(1e-3f));
            Assert.That(spear.RationsUnits, Is.EqualTo(12f).Within(1e-3f));
            Assert.That(spear.Starvation, Is.EqualTo(0.2f).Within(1e-3f));
            Assert.That(spear.Formed, Is.True, "结阵姿态必须存下来（COMBAT-17 影响战斗）");
        }

        [Test]
        public void RoundTrip_IsStable_SecondSerializeEqualsFirst()
        {
            var first = BattleSaveSerializer.Serialize(Sample());
            var second = BattleSaveSerializer.Serialize(BattleSaveSerializer.Deserialize(first));

            Assert.That(second, Is.EqualTo(first), "写→读→写 必须幂等（否则存档会漂移）");
        }

        [Test]
        public void Escaping_HandlesQuotesNewlinesAndBackslashes()
        {
            var save = Sample();
            // 用 unicode 转义序列表达，避免测试源码自身的转义嵌套歧义
            save.OutcomeReason = "\u0022引号\u0022、换行\n与反斜杠\u005C的文本";

            var restored = BattleSaveSerializer.Deserialize(BattleSaveSerializer.Serialize(save));

            Assert.That(restored.OutcomeReason, Is.EqualTo(save.OutcomeReason),
                "转义必须正确往返（编年史文案含中文标点与换行）");
        }

        [Test]
        public void ChineseCurrency_IsNotMangled()
        {
            var restored = BattleSaveSerializer.Deserialize(BattleSaveSerializer.Serialize(Sample()));

            Assert.That(restored.Chronicle[1].Text, Does.Contain("秦军斥候回报"));
            Assert.That(restored.Units[0].Label, Does.Contain("赵骑1"));
        }

        [Test]
        public void Version_IsReadable_ForMigration()
        {
            var restored = BattleSaveSerializer.Deserialize(BattleSaveSerializer.Serialize(new BattleSave()));

            Assert.That(restored.Version, Is.EqualTo(BattleSave.CurrentVersion),
                "版本号必须可读——日后升 schema 时按它迁移");
        }

        [Test]
        public void CorruptedInput_DegradesGracefully_WithoutThrowing()
        {
            Assert.That(() => BattleSaveSerializer.Deserialize(""), Throws.Nothing);
            Assert.That(() => BattleSaveSerializer.Deserialize("{ 这不是 JSON"), Throws.Nothing);
            Assert.That(() => BattleSaveSerializer.Deserialize("null"), Throws.Nothing);
            Assert.That(() => BattleSaveSerializer.Deserialize("{\"units\": [ { } ]}"), Throws.Nothing);

            // 截断的存档：能读出的字段照读，读不出的取默认值，绝不抛异常。
            string truncated = "{\"version\": 1, \"mapId\": \"changping_v1\", \"units\": [{\"id\": \"a\"";
            var restored = BattleSaveSerializer.Deserialize(truncated);
            Assert.That(restored.MapId, Is.EqualTo("changping_v1"));
        }

        [Test]
        public void EmptyCollections_RoundTrip()
        {
            var restored = BattleSaveSerializer.Deserialize(BattleSaveSerializer.Serialize(new BattleSave()));

            Assert.That(restored.Units, Is.Empty);
            Assert.That(restored.Chronicle, Is.Empty);
        }

        [Test]
        public void Numbers_UseInvariantCulture()
        {
            // 小数必须写成 "195.5" 而非某些区域设置的 "195,5"（否则 JSON 非法）。
            string json = BattleSaveSerializer.Serialize(Sample());

            Assert.That(json, Does.Contain("195.5"));
            Assert.That(json, Does.Not.Contain("195,5"));
        }
    }

}
