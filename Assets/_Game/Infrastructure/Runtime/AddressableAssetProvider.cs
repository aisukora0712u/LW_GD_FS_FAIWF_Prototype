using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Infrastructure
{
    public sealed class AddressableAssetProvider : IAssetProvider, IDisposable
    {
        private readonly Dictionary<object, AsyncOperationHandle> handles = new Dictionary<object, AsyncOperationHandle>();

        public async Task<T> LoadAsync<T>(object key, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (handles.ContainsKey(key))
            {
                throw new InvalidOperationException($"Addressable key '{key}' is already loaded by this provider.");
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            handles.Add(key, handle);
            try
            {
                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw handle.OperationException ?? new InvalidOperationException($"Failed to load addressable '{key}'.");
                }

                return handle.Result;
            }
            catch
            {
                Release(key);
                throw;
            }
        }

        public void Release(object key)
        {
            if (key != null && handles.TryGetValue(key, out var handle))
            {
                Addressables.Release(handle);
                handles.Remove(key);
            }
        }

        public void ReleaseAll()
        {
            foreach (var handle in handles.Values)
            {
                Addressables.Release(handle);
            }

            handles.Clear();
        }

        public void Dispose() => ReleaseAll();
    }
}
