using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AspireAiStack.ApiService;

internal static class AiTelemetry
{
    public const string SourceName = "AspireAiStack.ApiService";
    public const string MeterName = "AspireAiStack.ApiService";
    public const string AgentName = "grounded-answer-agent";
    public const string AgentVersion = "1.0.0";
    public const string WorkflowName = "grounded-rag-answer";
    public const string PromptVersion = "grounded-answer-v2";
    public const string KnowledgeCorpusVersion = "seeded-knowledge-v1";
    public const string InvokeWorkflowOperationName = "invoke_workflow";
    public const string SearchToolName = "search_knowledge";
    public const string ExecuteToolOperationName = "execute_tool";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "ai.requests", "{request}", "Completed AI assistant requests.");
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
        "ai.cache.hits", "{hit}", "AI assistant requests served from the response cache.");
    public static readonly Histogram<int> RetrievalSources = Meter.CreateHistogram<int>(
        "ai.retrieval.sources", "{source}", "Number of grounding sources returned by retrieval.");
    public static readonly Histogram<int> AnswerLength = Meter.CreateHistogram<int>(
        "ai.answer.length", "{character}", "Length of the generated answer in characters.");

    public static void MarkSuccess(Activity? activity) =>
        activity?.SetStatus(ActivityStatusCode.Ok);

    public static void MarkError(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag("error.type", exception.GetType().FullName);
    }

    public static TagList RequestTags(string mode, bool cacheHit, string outcome) =>
        new()
        {
            { "ai.mode", mode },
            { "ai.cache_hit", cacheHit },
            { "ai.outcome", outcome }
        };
}
