using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Gameplay
{
    public readonly struct RunSaveResult
    {
        private RunSaveResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Error { get; }
        public static RunSaveResult Success() => new RunSaveResult(true, string.Empty);
        public static RunSaveResult Failure(string error) => new RunSaveResult(false, error);
    }

    public readonly struct RunLoadResult
    {
        private RunLoadResult(bool succeeded, RunSession session, bool recoveredFromBackup, string error)
        {
            Succeeded = succeeded;
            Session = session;
            RecoveredFromBackup = recoveredFromBackup;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public RunSession Session { get; }
        public bool RecoveredFromBackup { get; }
        public string Error { get; }
        public static RunLoadResult Success(RunSession session, bool recovered) => new RunLoadResult(true, session, recovered, string.Empty);
        public static RunLoadResult Failure(string error) => new RunLoadResult(false, null, false, error);
    }

    public sealed class RunSaveCoordinator
    {
        private readonly ContentCatalog catalog;
        private readonly ISaveService saves;

        public RunSaveCoordinator(ContentCatalog catalog, ISaveService saves)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        }

        public async Task<RunSaveResult> SaveAsync(string slot, RunSession session, CancellationToken cancellationToken)
        {
            if (session == null)
            {
                return RunSaveResult.Failure("A run session is required.");
            }

            var checkpoint = session.CreateCheckpoint();
            if (!checkpoint.Succeeded)
            {
                return RunSaveResult.Failure(checkpoint.Error);
            }

            try
            {
                var payload = RunCheckpointJson.Serialize(checkpoint.Checkpoint);
                await saves.SaveAsync(slot, RunCheckpoint.CurrentSchemaVersion, payload, cancellationToken);
                return RunSaveResult.Success();
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return RunSaveResult.Failure(exception.Message);
            }
        }

        public async Task<RunLoadResult> LoadAsync(string slot, CancellationToken cancellationToken)
        {
            var read = await saves.LoadAsync(slot, cancellationToken);
            if (!read.Succeeded)
            {
                return RunLoadResult.Failure(read.Error);
            }

            if (read.SchemaVersion != RunCheckpoint.CurrentSchemaVersion)
            {
                return RunLoadResult.Failure("Save schema version is not supported by the Run checkpoint loader.");
            }

            var decoded = RunCheckpointJson.Deserialize(read.PayloadJson);
            if (!decoded.Succeeded)
            {
                return RunLoadResult.Failure(decoded.Error);
            }

            if (!RunSession.TryRestore(catalog, decoded.Checkpoint, out var session, out var error))
            {
                return RunLoadResult.Failure(error);
            }

            return RunLoadResult.Success(session, read.RecoveredFromBackup);
        }
    }
}
