using System.Net.Http.Json;
using AspireAiStack.ApiService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AspireAiStack.Tests;

public sealed class ApiTests : IClassFixture<ApiTests.ApiFactory>
{
    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Status_ReportsCredentialFreeDefaults()
    {
        var status = await _client.GetFromJsonAsync<StackStatus>(
            "/api/status",
            CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("simulated", status.AiMode);
        Assert.Equal("in-memory", status.Cache);
        Assert.True(status.SafeDefault);
    }

    [Fact]
    public async Task KnowledgeTopics_ExposeTheSeededCatalogQuestions()
    {
        var topics = await _client.GetFromJsonAsync<IReadOnlyList<KnowledgeTopic>>(
            "/api/knowledge/topics",
            CancellationToken.None);

        Assert.NotNull(topics);
        Assert.Equal(KnowledgeCatalog.Documents.Count, topics.Count);
        Assert.Equal(
            KnowledgeCatalog.Documents.Select(document => document.Title),
            topics.Select(topic => topic.Title));
        Assert.All(topics, topic => Assert.False(string.IsNullOrWhiteSpace(topic.SuggestedQuestion)));
    }

    [Theory]
    [InlineData("Development", "AI:CaptureTelemetryContent", true)]
    [InlineData("Development", "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", true)]
    [InlineData("Production", "AI:CaptureTelemetryContent", false)]
    [InlineData("Production", "OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", false)]
    public async Task ContentCapture_RequiresDevelopment(
        string environment, string setting, bool expected)
    {
        using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Infrastructure:UseRedis", "false");
            builder.UseSetting("Infrastructure:UseQdrant", "false");
            builder.UseSetting("AI:Mode", "simulated");
            builder.UseSetting("AI:CaptureTelemetryContent", "false");
            builder.UseSetting("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false");
            builder.UseSetting(setting, "true");
        });
        using var client = configured.CreateClient();
        var status = await client.GetFromJsonAsync<StackStatus>("/api/status");
        Assert.NotNull(status);
        Assert.Equal(expected, status.SensitiveTelemetryEnabled);
    }

    [Fact]
    public async Task Chat_ReturnsGroundedAnswerThenCacheHit()
    {
        var prompt = $"How does Aspire wiring work? {Guid.NewGuid():N}";

        var first = await PostPromptAsync(prompt);
        var second = await PostPromptAsync(prompt);

        Assert.False(first.CacheHit);
        Assert.True(second.CacheHit);
        Assert.Equal(3, first.Sources.Count);
        Assert.Equal(first.Answer, second.Answer);
    }

    [Fact]
    public async Task Chat_RejectsBlankPrompt()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/chat",
            new ChatRequest(" "),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<ChatResponse> PostPromptAsync(string prompt)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/chat",
            new ChatRequest(prompt),
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>(CancellationToken.None)
            ?? throw new InvalidOperationException("Expected a chat response.");
    }

    public sealed class ApiFactory : WebApplicationFactory<ApiAssemblyMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Infrastructure:UseRedis"] = "false",
                    ["Infrastructure:UseQdrant"] = "false",
                    ["AI:Mode"] = "simulated"
                }));
        }
    }
}
