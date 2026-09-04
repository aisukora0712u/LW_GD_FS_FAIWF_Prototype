using System;
using Game.Foundation;

namespace Game.Infrastructure
{
    public sealed class GameContext : IDisposable
    {
        public GameContext(
            GameConfig config,
            IPlatformServices platform,
            ISaveService saves,
            ISceneFlowService scenes,
            IAssetProvider assets,
            IInputService input,
            IBuildInfo buildInfo)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            BuildInfo = buildInfo ?? throw new ArgumentNullException(nameof(buildInfo));
        }

        public GameConfig Config { get; }
        public IPlatformServices Platform { get; }
        public ISaveService Saves { get; }
        public ISceneFlowService Scenes { get; }
        public IAssetProvider Assets { get; }
        public IInputService Input { get; }
        public IBuildInfo BuildInfo { get; }

        public void Dispose()
        {
            Assets.ReleaseAll();
            UnityEngine.Object.Destroy(Config);
        }
    }
}
