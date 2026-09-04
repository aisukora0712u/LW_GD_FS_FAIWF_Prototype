using System.Threading;
using Game.Foundation;
using UnityEngine;

namespace Game.Infrastructure
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class CompositionRoot : MonoBehaviour
    {
        [SerializeField] private GameConfig sourceConfig;
        [SerializeField] private PlayerInputService inputService;

        private CancellationTokenSource lifetime;

        public GameContext Context { get; private set; }

        private async void Awake()
        {
            if (sourceConfig == null || inputService == null)
            {
                Debug.LogError("CompositionRoot is missing required references.", this);
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            lifetime = new CancellationTokenSource();
            var runtimeConfig = Instantiate(sourceConfig);
            runtimeConfig.name = sourceConfig.name + " (Runtime)";

            var platform = new OfflinePlatformServices();
            Context = new GameContext(
                runtimeConfig,
                platform,
                new JsonSaveService(),
                new SceneFlowService(),
                new AddressableAssetProvider(),
                inputService,
                new BuildInfoProvider());

            try
            {
                await platform.InitializeAsync(lifetime.Token);
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private void Update() => Context?.Platform.RunCallbacks();

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            Context?.Dispose();
            Context = null;
        }
    }
}
