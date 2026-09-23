using System.Diagnostics;

namespace AspireAiStack.ApiService;

public sealed class AiAssistantService(
    IKnowledgeStore knowledgeStore,
    IResponseCache responseCache,
    IAnswerGenerator answerGenerator)
{
    private static readonly ActivitySource ActivitySource = new("AspireAiStack.ApiService");

    public async Task<ChatResponse> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("answer grounded question");
        activity?.SetTag("ai.mode", answerGenerator.Mode);
        activity?.SetTag("ai.vector_store", knowledgeStore.Name);
        activity?.SetTag("ai.cache", responseCache.Name);

        var cached = await responseCache.GetAsync(prompt, cancellationToken);
        if (cached is not null)
        {
            activity?.SetTag("ai.cache_hit", true);
            return cached with { CacheHit = true };
        }

        activity?.SetTag("ai.cache_hit", false);
        var sources = await knowledgeStore.SearchAsync(prompt, limit: 3, cancellationToken);
        var answer = await answerGenerator.GenerateAsync(prompt, sources, cancellationToken);
        var response = new ChatResponse(
            answer,
            answerGenerator.Mode,
            knowledgeStore.Name,
            responseCache.Name,
            CacheHit: false,
            sources);

        await responseCache.SetAsync(prompt, response, cancellationToken);
        return response;
    }
}
