using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class JsonSaveServiceTests
    {
        private string directory;
        private JsonSaveService service;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "game-save-tests", System.Guid.NewGuid().ToString("N"));
            service = new JsonSaveService(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public async Task SaveAndLoad_RoundTripsEnvelope()
        {
            await service.SaveAsync("slot_1", 3, "{\"health\":42}", CancellationToken.None);
            var result = await service.LoadAsync("slot_1", CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.RecoveredFromBackup, Is.False);
            Assert.That(result.SchemaVersion, Is.EqualTo(3));
            Assert.That(result.PayloadJson, Is.EqualTo("{\"health\":42}"));
        }

        [Test]
        public async Task Load_CorruptPrimary_RecoversPreviousBackup()
        {
            await service.SaveAsync("slot_1", 1, "{\"chapter\":1}", CancellationToken.None);
            await service.SaveAsync("slot_1", 2, "{\"chapter\":2}", CancellationToken.None);
            File.WriteAllText(Path.Combine(directory, "slot_1.json"), "corrupt");

            var result = await service.LoadAsync("slot_1", CancellationToken.None);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.RecoveredFromBackup, Is.True);
            Assert.That(result.SchemaVersion, Is.EqualTo(1));
        }

        [TestCase("../escape")]
        [TestCase("slot with spaces")]
        [TestCase("")]
        public void InvalidSlot_IsRejected(string slot)
        {
            Assert.That(() => service.Exists(slot), Throws.ArgumentException);
        }
    }
}
