using Azure.Core;

namespace RxMind.Agents;

// Wraps any TokenCredential and caches the token until 5 minutes before expiry.
// Prevents repeated az account get-access-token subprocess calls on every LLM request.
public class CachedTokenCredential : TokenCredential
{
    private readonly TokenCredential _inner;
    private AccessToken _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CachedTokenCredential(TokenCredential inner) => _inner = inner;

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (_cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            return _cached;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock — another thread may have refreshed it
            if (_cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                return _cached;

            _cached = await _inner.GetTokenAsync(requestContext, cancellationToken);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }
}
