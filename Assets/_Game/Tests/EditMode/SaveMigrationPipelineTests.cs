using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SaveMigrationPipelineTests
    {
        [Test]
        public void Migrate_AppliesEveryIntermediateVersionInOrder()
        {
            var pipeline = new SaveMigrationPipeline(3, new ISaveMigration[]
            {
                new AppendMigration(1, "-v2"),
                new AppendMigration(2, "-v3")
            });

            Assert.That(pipeline.Migrate(1, "save"), Is.EqualTo("save-v2-v3"));
        }

        [Test]
        public void Migrate_MissingIntermediateVersion_IsRejected()
        {
            var pipeline = new SaveMigrationPipeline(3, new ISaveMigration[] { new AppendMigration(1, "-v2") });
            Assert.That(() => pipeline.Migrate(1, "save"), Throws.InvalidOperationException);
        }

        [Test]
        public void Migrate_FutureSave_IsRejected()
        {
            var pipeline = new SaveMigrationPipeline(2, new ISaveMigration[] { new AppendMigration(1, "-v2") });
            Assert.That(() => pipeline.Migrate(3, "save"), Throws.InvalidOperationException);
        }

        private sealed class AppendMigration : ISaveMigration
        {
            private readonly string suffix;

            public AppendMigration(int fromVersion, string suffix)
            {
                FromVersion = fromVersion;
                ToVersion = fromVersion + 1;
                this.suffix = suffix;
            }

            public int FromVersion { get; }
            public int ToVersion { get; }
            public string Migrate(string payloadJson) => payloadJson + suffix;
        }
    }
}
