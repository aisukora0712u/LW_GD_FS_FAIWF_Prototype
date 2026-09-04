using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure
{
    public sealed class SceneFlowService : ISceneFlowService
    {
        public async Task LoadLevelAsync(string sceneName, IProgress<float> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name is required.", nameof(sceneName));
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                throw new InvalidOperationException($"Unity could not start loading scene '{sceneName}'.");
            }

            while (!operation.isDone)
            {
                progress?.Report(operation.progress);
                await Task.Yield();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await UnloadAsync(sceneName, CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                throw new InvalidOperationException($"Scene '{sceneName}' did not finish loading.");
            }

            SceneManager.SetActiveScene(loadedScene);
            progress?.Report(1f);
        }

        public async Task UnloadAsync(string sceneName, CancellationToken cancellationToken)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var operation = SceneManager.UnloadSceneAsync(scene);
            while (operation != null && !operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
    }
}
