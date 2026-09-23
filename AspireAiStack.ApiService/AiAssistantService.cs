namespace AspireAiStack.ApiService;

public sealed class AiAssistantService(
    IKnowledgeStore knowledgeStore,
    IResponseCache responseCache,
    IAnswerGenerator answerGenerator)
{
    public async Task<ChatResponse> AskAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "unavailable";
        using var workflowActivity = AiTelemetry.ActivitySource.StartActivity(
            $"{AiTelemetry.InvokeWorkflowOperationName} {AiTelemetry.WorkflowName}",
            System.Diagnostics.ActivityKind.Internal,
            default(System.Diagnostics.ActivityContext),
            new[]
            {
                new KeyValuePair<string, object?>("gen_ai.operation.name", AiTelemetry.InvokeWorkflowOperationName),
                new KeyValuePair<string, object?>("gen_ai.workflow.name", AiTelemetry.WorkflowName),
                new KeyValuePair<string, object?>("ai.prompt.version", AiTelemetry.PromptVersion),
                new KeyValuePair<string, object?>("ai.knowledge_corpus.version", AiTelemetry.KnowledgeCorpusVersion),
                new KeyValuePair<string, object?>("ai.request.id", requestId)
            });

        using var activity = AiTelemetry.ActivitySource.StartActivity(
            $"invoke_agent {AiTelemetry.AgentName}",
            System.Diagnostics.ActivityKind.Internal,
            default(System.Diagnostics.ActivityContext),
            new[]
            {
                new KeyValuePair<string, object?>("gen_ai.operation.name", "invoke_agent"),
                new KeyValuePair<string, object?>("gen_ai.agent.name", AiTelemetry.AgentName),
                new KeyValuePair<string, object?>("gen_ai.agent.version", AiTelemetry.AgentVersion),
                new KeyValuePair<string, object?>("gen_ai.workflow.name", AiTelemetry.WorkflowName),
                new KeyValuePair<string, object?>("gen_ai.agent.description", "Retrieves seeded knowledge and produces a grounded answer."),
                new KeyValuePair<string, object?>("ai.mode", answerGenerator.Mode),
                new KeyValuePair<string, object?>("ai.vector_store", knowledgeStore.Name),
                new KeyValuePair<string, object?>("ai.cache", responseCache.Name),
                new KeyValuePair<string, object?>("ai.prompt.version", AiTelemetry.PromptVersion),
                new KeyValuePair<string, object?>("ai.knowledge_corpus.version", AiTelemetry.KnowledgeCorpusVersion),
                new KeyValuePair<string, object?>("ai.request.id", requestId)
            });

        try
        {
            var cached = await responseCache.GetAsync(prompt, cancellationToken);
            if (cached is not null)
            {
                activity?.SetTag("ai.cache_hit", true);
                activity?.SetTag("ai.source_count", cached.Sources.Count);
                workflowActivity?.SetTag("ai.cache_hit", true);
                workflowActivity?.SetTag("ai.source_count", cached.Sources.Count);
                AiTelemetry.Requests.Add(1, AiTelemetry.RequestTags(answerGenerator.Mode, cacheHit: true, "success"));
                AiTelemetry.CacheHits.Add(1, new KeyValuePair<string, object?>("ai.mode", answerGenerator.Mode));
                AiTelemetry.RetrievalSources.Record(cached.Sources.Count, new KeyValuePair<string, object?>("ai.cache_hit", true));
                AiTelemetry.AnswerLength.Record(cached.Answer.Length, new KeyValuePair<string, object?>("ai.cache_hit", true));
                AiTelemetry.MarkSuccess(activity);
                AiTelemetry.MarkSuccess(workflowActivity);
                return cached with { CacheHit = true };
            }

            activity?.SetTag("ai.cache_hit", false);
            workflowActivity?.SetTag("ai.cache_hit", false);
            var generated = await answerGenerator.GenerateAsync(prompt, cancellationToken);
            var response = new ChatResponse(
                generated.Text,
                answerGenerator.Mode,
                knowledgeStore.Name,
                responseCache.Name,
                CacheHit: false,
                generated.Sources);

            activity?.SetTag("ai.source_count", generated.Sources.Count);
            workflowActivity?.SetTag("ai.source_count", generated.Sources.Count);
            await responseCache.SetAsync(prompt, response, cancellationToken);
            AiTelemetry.Requests.Add(1, AiTelemetry.RequestTags(answerGenerator.Mode, cacheHit: false, "success"));
            AiTelemetry.RetrievalSources.Record(generated.Sources.Count, new KeyValuePair<string, object?>("ai.cache_hit", false));
            AiTelemetry.AnswerLength.Record(generated.Text.Length, new KeyValuePair<string, object?>("ai.cache_hit", false));
            AiTelemetry.MarkSuccess(activity);
            AiTelemetry.MarkSuccess(workflowActivity);
            return response;
        }
        catch (Exception exception)
        {
            AiTelemetry.Requests.Add(1, AiTelemetry.RequestTags(answerGenerator.Mode, cacheHit: false, "error"));
            AiTelemetry.MarkError(activity, exception);
            AiTelemetry.MarkError(workflowActivity, exception);
            throw;
        }
    }
}
