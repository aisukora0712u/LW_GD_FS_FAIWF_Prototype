using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Infrastructure
{
    public sealed class OfflinePlatformServices : IPlatformServices
    {
        public bool IsAvailable => false;
        public string UserId => "offline";

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<bool> UnlockAchievementAsync(string achievementId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public void RunCallbacks()
        {
        }
    }
}
