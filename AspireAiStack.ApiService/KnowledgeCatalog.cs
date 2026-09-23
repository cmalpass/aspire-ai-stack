using System.Security.Cryptography;
using System.Text;

namespace AspireAiStack.ApiService;

public static class KnowledgeCatalog
{
    public const int VectorDimensions = 32;

    public static IReadOnlyList<KnowledgeDocument> Documents { get; } =
    [
        new(1, "AppHost orchestration",
            "The Aspire AppHost declares services, containers, dependencies, startup order, and endpoints in one application model."),
        new(2, "Service references",
            "WithReference passes connection information to a consuming service. WaitFor delays startup until a dependency reports ready or healthy."),
        new(3, "Built-in observability",
            "Aspire service defaults emit logs, metrics, and distributed traces through OpenTelemetry and display them in the Aspire dashboard."),
        new(4, "Local AI development",
            "A local Ollama resource can provide a model endpoint without placing provider credentials in a browser or client application."),
        new(5, "Production boundaries",
            "The AppHost application model is reusable, but production deployments still require deliberate choices for identity, persistence, scaling, backups, and managed services."),
        new(6, "Vector search",
            "Qdrant stores embedding vectors and payloads. Similarity search retrieves relevant grounding passages before the model generates an answer."),
        new(7, "Caching",
            "Redis can cache repeated responses and shared application state, but cache keys must account for the prompt, model, and grounding context."),
    ];

    public static float[] Embed(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var vector = new float[VectorDimensions];
        var tokens = text.Split(
            [' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '(', ')', '/', '-'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()));
            var index = bytes[0] % VectorDimensions;
            var magnitude = 1f + (bytes[2] / 255f);
            vector[index] += (bytes[1] & 1) == 0 ? magnitude : -magnitude;
        }

        var norm = MathF.Sqrt(vector.Sum(value => value * value));
        if (norm == 0)
        {
            return vector;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= norm;
        }

        return vector;
    }

    public static float CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Vectors must have the same dimensions.");
        }

        var dot = 0f;
        var leftNorm = 0f;
        var rightNorm = 0f;

        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        return leftNorm == 0 || rightNorm == 0
            ? 0
            : dot / (MathF.Sqrt(leftNorm) * MathF.Sqrt(rightNorm));
    }
}
