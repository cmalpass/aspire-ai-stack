using AspireAiStack.ApiService;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var useRedis = builder.Configuration.GetValue("Infrastructure:UseRedis", false)
    && !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("cache"));
var useQdrant = builder.Configuration.GetValue("Infrastructure:UseQdrant", false)
    && !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("qdrant"));
var aiMode = builder.Configuration["AI:Mode"] ?? "simulated";

if (useRedis)
{
    builder.AddRedisClient("cache");
    builder.Services.AddSingleton<IResponseCache, RedisResponseCache>();
}
else
{
    builder.Services.AddSingleton<IResponseCache, MemoryResponseCache>();
}

if (useQdrant)
{
    builder.AddQdrantClient("qdrant");
    builder.Services.AddSingleton<IKnowledgeStore, QdrantKnowledgeStore>();
}
else
{
    builder.Services.AddSingleton<IKnowledgeStore, InMemoryKnowledgeStore>();
}

if (string.Equals(aiMode, "ollama", StringComparison.OrdinalIgnoreCase))
{
    var ollamaEndpoint = builder.Configuration.GetConnectionString("ollama")
        ?? builder.Configuration.GetConnectionString("chat-model")
        ?? throw new InvalidOperationException("Ollama mode requires an Aspire Ollama resource reference.");
    var model = builder.Configuration["AI:Model"] ?? "phi3:mini";

    builder.Services.AddChatClient(services =>
        ((IChatClient)new OllamaApiClient(OllamaConnectionString.ResolveEndpoint(ollamaEndpoint), model))
            .AsBuilder()
            .UseOpenTelemetry(services.GetRequiredService<ILoggerFactory>(), sourceName: "AspireAiStack.AI")
            .Build());
    builder.Services.AddSingleton<IAnswerGenerator, OllamaAnswerGenerator>();
}
else
{
    builder.Services.AddSingleton<IAnswerGenerator, SimulatedAnswerGenerator>();
}

builder.Services.AddSingleton<AiAssistantService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "Aspire AI Stack API",
    endpoints = new[] { "/api/status", "/api/chat" }
}));

app.MapGet("/api/status", (IKnowledgeStore knowledgeStore, IResponseCache responseCache) =>
    Results.Ok(new StackStatus(
        AiMode: aiMode,
        VectorStore: knowledgeStore.Name,
        Cache: responseCache.Name,
        SafeDefault: string.Equals(aiMode, "simulated", StringComparison.OrdinalIgnoreCase))));

app.MapPost("/api/chat", async (
    ChatRequest request,
    AiAssistantService assistant,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Prompt)] = ["Enter a question before submitting."]
        });
    }

    var response = await assistant.AskAsync(request.Prompt.Trim(), cancellationToken);
    return Results.Ok(response);
});

app.MapDefaultEndpoints();
app.Run();

public partial class Program;
