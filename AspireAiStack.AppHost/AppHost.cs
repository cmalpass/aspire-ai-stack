var builder = DistributedApplication.CreateBuilder(args);

var useContainers = !bool.TryParse(builder.Configuration["Demo:UseContainers"], out var configuredUseContainers)
    || configuredUseContainers;
var useLocalModel = bool.TryParse(builder.Configuration["Demo:UseLocalModel"], out var configuredUseLocalModel)
    && configuredUseLocalModel;

IResourceBuilder<ProjectResource> apiService;
IResourceBuilder<RedisResource>? cache = null;

if (useContainers)
{
    cache = builder.AddRedis("cache")
        .WithLifetime(ContainerLifetime.Persistent);

    var qdrant = builder.AddQdrant("qdrant")
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent);

    var ollama = builder.AddOllama("ollama")
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent);

    apiService = builder.AddProject<Projects.AspireAiStack_ApiService>("apiservice", launchProfileName: "http")
        .WithHttpHealthCheck("/health")
        .WithReference(cache)
        .WithReference(qdrant)
        .WithReference(ollama)
        .WithEnvironment("Infrastructure__UseRedis", "true")
        .WithEnvironment("Infrastructure__UseQdrant", "true")
        .WithEnvironment("AI__Mode", useLocalModel ? "ollama" : "simulated")
        .WithEnvironment("AI__Model", "phi3:mini")
        .WaitFor(cache)
        .WaitFor(qdrant)
        .WaitFor(ollama);

    if (useLocalModel)
    {
        var chatModel = ollama.AddModel("chat-model", "phi3:mini");
        apiService
            .WithReference(chatModel)
            .WaitFor(chatModel);
    }
}
else
{
    apiService = builder.AddProject<Projects.AspireAiStack_ApiService>("apiservice", launchProfileName: "http")
        .WithHttpHealthCheck("/health")
        .WithEnvironment("Infrastructure__UseRedis", "false")
        .WithEnvironment("Infrastructure__UseQdrant", "false")
        .WithEnvironment("AI__Mode", "simulated");
}

var web = builder.AddProject<Projects.AspireAiStack_Web>("webfrontend", launchProfileName: "http")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

if (cache is not null)
{
    web
        .WithReference(cache)
        .WaitFor(cache);
}

builder.Build().Run();
