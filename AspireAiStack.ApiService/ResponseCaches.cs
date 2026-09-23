using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace AspireAiStack.ApiService;

public interface IResponseCache
{
    string Name { get; }

    Task<ChatResponse?> GetAsync(string prompt, CancellationToken cancellationToken);

    Task SetAsync(string prompt, ChatResponse response, CancellationToken cancellationToken);
}

public sealed class MemoryResponseCache : IResponseCache
{
    private readonly ConcurrentDictionary<string, ChatResponse> _cache = new(StringComparer.Ordinal);

    public string Name => "in-memory";

    public Task<ChatResponse?> GetAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache.TryGetValue(CreateKey(prompt), out var response);
        return Task.FromResult(response);
    }

    public Task SetAsync(string prompt, ChatResponse response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cache[CreateKey(prompt)] = response;
        return Task.CompletedTask;
    }

    internal static string CreateKey(string prompt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(prompt.Trim().ToLowerInvariant()));
        return $"chat:{Convert.ToHexString(bytes)}";
    }
}

public sealed class RedisResponseCache(IConnectionMultiplexer connection) : IResponseCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public string Name => "Redis";

    public async Task<ChatResponse?> GetAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await connection.GetDatabase().StringGetAsync(MemoryResponseCache.CreateKey(prompt));
        return value.HasValue
            ? JsonSerializer.Deserialize<ChatResponse>(value.ToString(), JsonOptions)
            : null;
    }

    public async Task SetAsync(string prompt, ChatResponse response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = JsonSerializer.Serialize(response, JsonOptions);
        await connection.GetDatabase().StringSetAsync(
            MemoryResponseCache.CreateKey(prompt),
            payload,
            CacheDuration);
    }
}
