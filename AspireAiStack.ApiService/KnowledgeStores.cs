using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AspireAiStack.ApiService;

public interface IKnowledgeStore
{
    string Name { get; }

    Task<IReadOnlyList<SourceCitation>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly IReadOnlyList<(KnowledgeDocument Document, float[] Vector)> _documents =
        KnowledgeCatalog.Documents
            .Select(document => (document, KnowledgeCatalog.Embed($"{document.Title} {document.Content}")))
            .ToArray();

    public string Name => "in-memory deterministic vectors";

    public Task<IReadOnlyList<SourceCitation>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queryVector = KnowledgeCatalog.Embed(query);

        IReadOnlyList<SourceCitation> results = _documents
            .Select(item => new SourceCitation(
                item.Document.Title,
                item.Document.Content,
                KnowledgeCatalog.CosineSimilarity(queryVector, item.Vector)))
            .OrderByDescending(result => result.Relevance)
            .Take(limit)
            .ToArray();

        return Task.FromResult(results);
    }
}

public sealed class QdrantKnowledgeStore(
    QdrantClient client,
    ILogger<QdrantKnowledgeStore> logger) : IKnowledgeStore
{
    private const string CollectionName = "aspire_knowledge";
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public string Name => "Qdrant";

    public async Task<IReadOnlyList<SourceCitation>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        var points = await client.SearchAsync(
            CollectionName,
            KnowledgeCatalog.Embed(query),
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        return points
            .Select(point => new SourceCitation(
                point.Payload["title"].StringValue,
                point.Payload["content"].StringValue,
                point.Score))
            .ToArray();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            if (!await client.CollectionExistsAsync(CollectionName, cancellationToken))
            {
                await client.CreateCollectionAsync(
                    CollectionName,
                    new VectorParams
                    {
                        Size = KnowledgeCatalog.VectorDimensions,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);
            }

            var points = KnowledgeCatalog.Documents
                .Select(document => new PointStruct
                {
                    Id = document.Id,
                    Vectors = KnowledgeCatalog.Embed($"{document.Title} {document.Content}"),
                    Payload =
                    {
                        ["title"] = document.Title,
                        ["content"] = document.Content
                    }
                })
                .ToArray();

            await client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);
            _initialized = true;
            logger.LogInformation("Seeded {Count} knowledge records into {Collection}.", points.Length, CollectionName);
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
