using System.Collections.Concurrent;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._User.DTOs.Storages
{
    public class JwtBlacklistStorage : ConcurrentDictionary<string, BlacklistedToken>, ISelfSingletonDependency
    {
        private const int CleanupInterval = 60_000; 
        private readonly System.Timers.Timer _cleanupTimer;

        public JwtBlacklistStorage()
        {
            _cleanupTimer = new System.Timers.Timer(CleanupInterval);
            _cleanupTimer.Elapsed += (sender, args) => RemoveExpiredTokens();
            _cleanupTimer.Start();
        }

        public void AddToken(string token, DateTime expiry)
        {
            this[token] = new BlacklistedToken
            {
                Token = token,
                Expiry = expiry,
                AddedMoment = DateTime.UtcNow
            };
        }

        public bool IsTokenBlacklisted(string token)
        {
            if (TryGetValue(token, out var data))
            {
                if (data.Expiry > DateTime.UtcNow)
                    return true;
                else
                    TryRemove(token, out _); 
            }
            return false;
        }

        public bool RemoveToken(string token)
        {
            return TryRemove(token, out _);
        }

        private void RemoveExpiredTokens()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in this)
            {
                if (kvp.Value.Expiry <= now)
                {
                    TryRemove(kvp.Key, out _);
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
        }
    }

    public class BlacklistedToken
    {
        public string Token { get; set; }
        public DateTime Expiry { get; set; }
        public DateTime AddedMoment { get; set; }
    }

}
