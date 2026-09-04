using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Foundation
{
    public sealed class SaveMigrationPipeline
    {
        private readonly int currentVersion;
        private readonly IReadOnlyDictionary<int, ISaveMigration> migrations;

        public SaveMigrationPipeline(int currentVersion, IEnumerable<ISaveMigration> migrations)
        {
            if (currentVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion));
            }

            this.currentVersion = currentVersion;
            var items = (migrations ?? throw new ArgumentNullException(nameof(migrations))).ToArray();
            if (items.Any(item => item == null || item.ToVersion != item.FromVersion + 1))
            {
                throw new ArgumentException("Every migration must advance exactly one schema version.", nameof(migrations));
            }

            if (items.GroupBy(item => item.FromVersion).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("Only one migration is allowed from each schema version.", nameof(migrations));
            }

            this.migrations = items.ToDictionary(item => item.FromVersion);
        }

        public string Migrate(int sourceVersion, string payloadJson)
        {
            if (sourceVersion < 1 || sourceVersion > currentVersion)
            {
                throw new InvalidOperationException($"Unsupported save schema {sourceVersion}; current schema is {currentVersion}.");
            }

            var version = sourceVersion;
            var migrated = payloadJson ?? throw new ArgumentNullException(nameof(payloadJson));
            while (version < currentVersion)
            {
                if (!migrations.TryGetValue(version, out var migration))
                {
                    throw new InvalidOperationException($"Missing save migration from schema {version} to {version + 1}.");
                }

                migrated = migration.Migrate(migrated) ?? throw new InvalidOperationException($"Migration from schema {version} returned null.");
                version = migration.ToVersion;
            }

            return migrated;
        }
    }
}
