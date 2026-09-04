using UnityEngine;

namespace Game.Foundation
{
    [CreateAssetMenu(menuName = "Game/Production/Level Profile", fileName = "LevelProfile")]
    public sealed class LevelProductionProfile : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string sceneName;
        [SerializeField] private string owner;
        [SerializeField, Min(1)] private int targetFramesPerSecond = 60;
        [SerializeField, Min(1)] private int maximumResidentMemoryMb = 4096;
        [SerializeField, Min(0.1f)] private float maximumLoadTimeSeconds = 10f;
        [SerializeField, Min(0f)] private float maximumGcAllocPerFrameKb;
        [SerializeField, TextArea] private string lightingStrategy = "Baked lighting with mixed main light.";
        [SerializeField, TextArea] private string navigationStrategy = "Baked NavMesh per gameplay scene.";

        public string StableId => stableId;
        public string SceneName => sceneName;
        public string Owner => owner;
        public int TargetFramesPerSecond => targetFramesPerSecond;
        public int MaximumResidentMemoryMb => maximumResidentMemoryMb;
        public float MaximumLoadTimeSeconds => maximumLoadTimeSeconds;
        public float MaximumGcAllocPerFrameKb => maximumGcAllocPerFrameKb;
        public string LightingStrategy => lightingStrategy;
        public string NavigationStrategy => navigationStrategy;
    }
}
