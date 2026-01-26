using Microsoft.Extensions.Caching.Memory;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class NonceService(IMemoryCache cache) : INonceService, ISingletonDependency
    {
        public bool TryUse(string nonce, TimeSpan ttl)
        {
            if (cache.TryGetValue(nonce, out _))
                return false;

            cache.Set(nonce, true, ttl);
            return true;
        }
    }
}
