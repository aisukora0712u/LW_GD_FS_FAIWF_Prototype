using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Foundation
{
    public interface IPlatformServices
    {
        bool IsAvailable { get; }
        string UserId { get; }
        Task InitializeAsync(CancellationToken cancellationToken);
        Task<bool> UnlockAchievementAsync(string achievementId, CancellationToken cancellationToken);
        void RunCallbacks();
    }

    public interface ISaveService
    {
        Task SaveAsync(string slot, int schemaVersion, string payloadJson, CancellationToken cancellationToken);
        Task<SaveReadResult> LoadAsync(string slot, CancellationToken cancellationToken);
        bool Exists(string slot);
        void Delete(string slot);
    }

    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        string Migrate(string payloadJson);
    }

    public interface ISceneFlowService
    {
        Task LoadLevelAsync(string sceneName, IProgress<float> progress, CancellationToken cancellationToken);
        Task UnloadAsync(string sceneName, CancellationToken cancellationToken);
    }

    public interface IAssetProvider
    {
        Task<T> LoadAsync<T>(object key, CancellationToken cancellationToken) where T : UnityEngine.Object;
        void Release(object key);
        void ReleaseAll();
    }

    public interface IInputService
    {
        event Action<string> ControlSchemeChanged;
        string CurrentControlScheme { get; }
        void SetGameplayEnabled(bool enabled);
    }

    public interface IBuildInfo
    {
        string Version { get; }
        string BuildNumber { get; }
        string Commit { get; }
        string Channel { get; }
        string UnityVersion { get; }
    }

    public readonly struct SaveReadResult
    {
        private SaveReadResult(bool succeeded, bool recoveredFromBackup, int schemaVersion, string payloadJson, string error)
        {
            Succeeded = succeeded;
            RecoveredFromBackup = recoveredFromBackup;
            SchemaVersion = schemaVersion;
            PayloadJson = payloadJson;
            Error = error;
        }

        public bool Succeeded { get; }
        public bool RecoveredFromBackup { get; }
        public int SchemaVersion { get; }
        public string PayloadJson { get; }
        public string Error { get; }

        public static SaveReadResult Success(int schemaVersion, string payloadJson, bool recovered) =>
            new SaveReadResult(true, recovered, schemaVersion, payloadJson, string.Empty);

        public static SaveReadResult Failure(string error) =>
            new SaveReadResult(false, false, 0, string.Empty, error ?? "Unknown save error.");
    }

}
