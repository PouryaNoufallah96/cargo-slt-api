using System.Collections.Concurrent;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._User.DTOs.Storages
{
    public class UserAuthStorage : ConcurrentDictionary<string, UserAuthData>, ISelfSingletonDependency 
    {
        private const int CleanupInterval = 60_000; 
        private readonly System.Timers.Timer _cleanupTimer;
        private readonly ConcurrentDictionary<string, object> _locks = new();

        public void AddItem(string key, UserAuthData data)
        {
            this[key] = data;
        }

        public UserAuthStorage()
        {
            _cleanupTimer = new System.Timers.Timer(CleanupInterval);
            _cleanupTimer.Elapsed += (sender, args) => {
                try { RemoveOldEntries(); }
                catch (Exception)
                {
                }
            };
            _cleanupTimer.Start();
        }

        public UserAuthData? GetItem(string nonce)
        {
            if (TryGetValue(nonce, out var data))
            {
                return data;
            }
            return null;
        }

        public bool RemoveItem(string nonceId)
        {
            var removed = TryRemove(nonceId, out _);
            if (removed)
            {
                _locks.TryRemove(nonceId, out _);
            }
            return removed;
        }


        private void RemoveOldEntries()
        {
            var threshold = DateTime.UtcNow.AddSeconds(-310);
            var keysToRemove = this.Where(kv => kv.Value.GeneratedMoment < threshold)
                                  .Select(kv => kv.Key)
                                  .ToList();

            foreach (var key in keysToRemove)
            {
                if (TryRemove(key, out _))
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
        }
    }


    public class UserAuthData
    {
        public DateTime GeneratedMoment { get; set; } = DateTime.UtcNow;
        public string Nonce { get; set; }
        public string WalletAddress { get; set; }
        public bool IsVerified { get; set; } = false;
        public string IP { get; set; }
    }

}
