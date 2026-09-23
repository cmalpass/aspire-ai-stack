using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Diagnostics;

namespace AspireAiStack.ApiService;

public interface IAnswerGenerator
{
    string Mode { get; }

    Task<GeneratedAnswer> GenerateAsync(string prompt, CancellationToken cancellationToken);
}

public sealed record GeneratedAnswer(string Text, IReadOnlyList<SourceCitation> Sources);

public sealed class SimulatedAnswerGenerator(IKnowledgeStore knowledgeStore) : IAnswerGenerator
{
    public string Mode => "simulated";

    public async Task<GeneratedAnswer> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var sources = await knowledgeStore.SearchAsync(prompt, limit: 3, cancellationToken);
        var topSource = sources.First();
        var answer = $"For ‘{prompt}’, start with {topSource.Title.ToLowerInvariant()}: {topSource.Content} "
            + "This deterministic response keeps the first run credential-free. Enable the Ollama model resource when you want a real local generation path.";
        return new GeneratedAnswer(answer, sources);
    }
}

public sealed class OllamaAnswerGenerator(
    IChatClient chatClient,
    IKnowledgeStore knowledgeStore) : IAnswerGenerator
{
    public string Mode => "ollama";

    public async Task<GeneratedAnswer> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var searchTool = new KnowledgeSearchTool(knowledgeStore);
        var searchFunction = AIFunctionFactory.Create(
            searchTool.SearchAsync,
            AiTelemetry.SearchToolName,
            "Search the seeded knowledge base for passages that can ground an answer.");

        var response = await chatClient.GetResponseAsync(
            $"""
            You are a grounded-answer agent. Call the search_knowledge tool exactly once using the user's question.
            Then answer only from the tool result. Be concise and cite the returned sources as [1], [2], or [3].
            Do not invent facts or citations.

            User question: {prompt}
            """,
            new ChatOptions
            {
                Tools = [searchFunction],
                ToolMode = ChatToolMode.RequireSpecific(AiTelemetry.SearchToolName),
                Temperature = 0.1f
            },
            cancellationToken: cancellationToken);

        return new GeneratedAnswer(response.Text ?? "The model returned no text.", searchTool.Sources);
    }
}

internal sealed class KnowledgeSearchTool(IKnowledgeStore knowledgeStore)
{
    public IReadOnlyList<SourceCitation> Sources { get; private set; } = [];

    [Description("Search the seeded knowledge base using a natural-language question.")]
    public async Task<string> SearchAsync(
        [Description("The user's question or search phrase.")] string query,
        CancellationToken cancellationToken = default)
    {
        var functionInvocationIsAlreadyInstrumented =
            string.Equals(
                Activity.Current?.GetTagItem("gen_ai.operation.name")?.ToString(),
                AiTelemetry.ExecuteToolOperationName,
                StringComparison.Ordinal);
        using var executeToolActivity = functionInvocationIsAlreadyInstrumented
            ? null
            : AiTelemetry.ActivitySource.StartActivity(
                $"{AiTelemetry.ExecuteToolOperationName} {AiTelemetry.SearchToolName}",
                ActivityKind.Internal,
                default(ActivityContext),
                new[]
                {
                    new KeyValuePair<string, object?>("gen_ai.operation.name", AiTelemetry.ExecuteToolOperationName),
                    new KeyValuePair<string, object?>("gen_ai.tool.type", "function"),
                    new KeyValuePair<string, object?>("gen_ai.tool.name", AiTelemetry.SearchToolName)
                });

        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "retrieval knowledge-store",
            ActivityKind.Internal,
            default(ActivityContext),
            new[]
            {
                new KeyValuePair<string, object?>("gen_ai.operation.name", "retrieval"),
                new KeyValuePair<string, object?>("gen_ai.tool.name", AiTelemetry.SearchToolName),
                new KeyValuePair<string, object?>("gen_ai.tool.type", "datastore"),
                new KeyValuePair<string, object?>("gen_ai.data_source.id", knowledgeStore.Name),
                new KeyValuePair<string, object?>("ai.query.length", query.Length)
            });

        try
        {
            Sources = await knowledgeStore.SearchAsync(query, limit: 3, cancellationToken);
            activity?.SetTag("ai.source_count", Sources.Count);
            executeToolActivity?.SetTag("ai.source_count", Sources.Count);
            AiTelemetry.MarkSuccess(activity);
            AiTelemetry.MarkSuccess(executeToolActivity);
        }
        catch (Exception exception)
        {
            AiTelemetry.MarkError(activity, exception);
            AiTelemetry.MarkError(executeToolActivity, exception);
            throw;
        }

        return string.Join(
            Environment.NewLine,
            Sources.Select((source, index) => $"[{index + 1}] {source.Title}: {source.Content}"));
    }
}
