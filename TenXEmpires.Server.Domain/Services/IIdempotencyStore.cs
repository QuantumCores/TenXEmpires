namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Store for idempotency keys to prevent duplicate operations.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to store a response for an idempotency key.
    /// </summary>
    /// <typeparam name="T">The type of response to store.</typeparam>
    /// <param name="key">The idempotency key (combination of route, user, and provided key).</param>
    /// <param name="response">The response to store.</param>
    /// <param name="expiration">How long to keep the entry in the store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if stored successfully, false if key already exists.</returns>
    Task<bool> TryStoreAsync<T>(
        string key,
        T response,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to retrieve a stored response for an idempotency key.
    /// </summary>
    /// <typeparam name="T">The type of response to retrieve.</typeparam>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored response if found, otherwise null.</returns>
    Task<T?> TryGetAsync<T>(
        string key,
        CancellationToken cancellationToken = default) where T : class;
}

