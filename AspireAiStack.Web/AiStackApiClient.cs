using System.Net.Http.Json;

namespace AspireAiStack.Web;

public sealed class AiStackApiClient(HttpClient httpClient)
{
    public async Task<StackStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<StackStatus>("/api/status", cancellationToken)
        ?? throw new InvalidOperationException("The API returned an empty status response.");

    public async Task<ChatResponse> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            new ChatRequest(prompt),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty chat response.");
    }
}

public sealed record ChatRequest(string Prompt);

public sealed record SourceCitation(string Title, string Content, float Relevance);

public sealed record ChatResponse(
    string Answer,
    string AiMode,
    string VectorStore,
    string Cache,
    bool CacheHit,
    IReadOnlyList<SourceCitation> Sources);

public sealed record StackStatus(string AiMode, string VectorStore, string Cache, bool SafeDefault);
