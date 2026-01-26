namespace Utilities.Services.Contracts
{
    public interface INonceService
    {
        bool TryUse(string nonce, TimeSpan ttl);

    }
}
