using AspireAiStack.ApiService;

namespace AspireAiStack.Tests;

public sealed class KnowledgeCatalogTests
{
    [Fact]
    public void Catalog_ContainsTheSevenTopicsShownOnTheHomepage()
    {
        Assert.Equal(7, KnowledgeCatalog.Documents.Count);
        Assert.Equal(
            [
                "AppHost orchestration",
                "Service references",
                "Built-in observability",
                "Local AI development",
                "Production boundaries",
                "Vector search",
                "Caching"
            ],
            KnowledgeCatalog.Documents.Select(document => document.Title));
    }

    [Fact]
    public void Embed_IsDeterministicAndNormalized()
    {
        var first = KnowledgeCatalog.Embed("Aspire connects application resources");
        var second = KnowledgeCatalog.Embed("Aspire connects application resources");

        Assert.Equal(KnowledgeCatalog.VectorDimensions, first.Length);
        Assert.Equal(first, second);
        Assert.InRange(MathF.Sqrt(first.Sum(value => value * value)), 0.999f, 1.001f);
    }

    [Fact]
    public async Task InMemoryStore_ReturnsTheRequestedNumberOfRankedSources()
    {
        var store = new InMemoryKnowledgeStore();

        var results = await store.SearchAsync("How does vector search work?", 3, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].Relevance >= results[1].Relevance);
        Assert.All(results, source => Assert.False(string.IsNullOrWhiteSpace(source.Content)));
    }

    [Fact]
    public async Task MemoryCache_NormalizesPromptKeys()
    {
        var cache = new MemoryResponseCache();
        var response = new ChatResponse("answer", "simulated", "memory", "memory", false, []);

        await cache.SetAsync("  What is Aspire? ", response, CancellationToken.None);
        var cached = await cache.GetAsync("what is aspire?", CancellationToken.None);

        Assert.Equal(response, cached);
    }
}
