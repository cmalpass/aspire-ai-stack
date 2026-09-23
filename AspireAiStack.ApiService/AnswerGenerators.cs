using Microsoft.Extensions.AI;

namespace AspireAiStack.ApiService;

public interface IAnswerGenerator
{
    string Mode { get; }

    Task<string> GenerateAsync(
        string prompt,
        IReadOnlyList<SourceCitation> sources,
        CancellationToken cancellationToken);
}

public sealed class SimulatedAnswerGenerator : IAnswerGenerator
{
    public string Mode => "simulated";

    public Task<string> GenerateAsync(
        string prompt,
        IReadOnlyList<SourceCitation> sources,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var topSource = sources.First();
        var answer = $"For ‘{prompt}’, start with {topSource.Title.ToLowerInvariant()}: {topSource.Content} "
            + "This deterministic response keeps the first run credential-free. Enable the Ollama model resource when you want a real local generation path.";
        return Task.FromResult(answer);
    }
}

public sealed class OllamaAnswerGenerator(IChatClient chatClient) : IAnswerGenerator
{
    public string Mode => "ollama";

    public async Task<string> GenerateAsync(
        string prompt,
        IReadOnlyList<SourceCitation> sources,
        CancellationToken cancellationToken)
    {
        var context = string.Join(
            Environment.NewLine,
            sources.Select((source, index) => $"[{index + 1}] {source.Title}: {source.Content}"));

        var response = await chatClient.GetResponseAsync(
            $"""
            Answer the question using only the supplied context. Be concise and cite sources as [1], [2], or [3].

            Context:
            {context}

            Question: {prompt}
            """,
            cancellationToken: cancellationToken);

        return response.Text ?? "The model returned no text.";
    }
}
