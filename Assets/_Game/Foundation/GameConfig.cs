using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(menuName = "Game/Configuration/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int schemaVersion = 1;
        [SerializeField] private string bootstrapScene = "Bootstrap";
        [SerializeField] private string persistentScene = "Persistent";
        [SerializeField] private string firstLevelScene = "Level_Example";
        [SerializeField] private bool diagnosticsEnabledByDefault;

        public int SchemaVersion => schemaVersion;
        public string BootstrapScene => bootstrapScene;
        public string PersistentScene => persistentScene;
        public string FirstLevelScene => firstLevelScene;
        public bool DiagnosticsEnabledByDefault => diagnosticsEnabledByDefault;
    }
}
