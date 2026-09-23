var builder = DistributedApplication.CreateBuilder(args);
const string localModel = "qwen2.5:3b";

var compose = builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(dashboard => dashboard.WithHostPort(18888));

compose
    .ConfigureComposeFile(file =>
    {
        file.Name = "aspire-ai-stack";

        // The checked-in Compose walkthrough is a local demo deployment. Keep
        // the same health endpoints available there as they are in Aspire's
        // Development dashboard so the evidence is easy to inspect.
        file.Services["apiservice"].Environment["DOTNET_ENVIRONMENT"] = "Development";
        file.Services["webfrontend"].Environment["DOTNET_ENVIRONMENT"] = "Development";
    });

var useContainers = !bool.TryParse(builder.Configuration["Demo:UseContainers"], out var configuredUseContainers)
    || configuredUseContainers;
var useLocalModel = bool.TryParse(builder.Configuration["Demo:UseLocalModel"], out var configuredUseLocalModel)
    ? configuredUseLocalModel
    : builder.ExecutionContext.IsPublishMode;
var captureTelemetryContent = bool.TryParse(builder.Configuration["Demo:CaptureTelemetryContent"], out var configuredCaptureTelemetryContent)
    && configuredCaptureTelemetryContent;

IResourceBuilder<ProjectResource> apiService;

if (useContainers)
{
    var cache = builder.AddRedis("cache")
        .WithLifetime(ContainerLifetime.Persistent);

    var qdrant = builder.AddQdrant("qdrant")
        .WithDataVolume("aspire-ai-stack-qdrant-data")
        .WithLifetime(ContainerLifetime.Persistent);

    var ollama = builder.AddOllama("ollama")
        .WithDataVolume("aspire-ai-stack-ollama-data")
        .WithLifetime(ContainerLifetime.Persistent);

    apiService = builder.AddProject<Projects.AspireAiStack_ApiService>("apiservice", launchProfileName: "http")
        .WithHttpHealthCheck("/health")
        .WithReference(cache)
        .WithReference(qdrant)
        .WithReference(ollama)
        .WithEnvironment("Infrastructure__UseRedis", "true")
        .WithEnvironment("Infrastructure__UseQdrant", "true")
        .WithEnvironment("AI__Mode", useLocalModel ? "ollama" : "simulated")
        .WithEnvironment("AI__Model", localModel)
        .WithEnvironment("AI__CaptureTelemetryContent", captureTelemetryContent ? "true" : "false")
        .WaitFor(cache)
        .WaitFor(qdrant)
        .WaitFor(ollama);

    if (useLocalModel)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            var modelLoader = builder.AddContainer("chat-model-loader", "ollama/ollama", "0.32.15")
                .WithArgs("pull", localModel)
                .WithEnvironment("OLLAMA_HOST", ollama.GetEndpoint("http"))
                .WithVolume("aspire-ai-stack-ollama-data", "/root/.ollama")
                .WaitFor(ollama);

            apiService.WaitForCompletion(modelLoader);
        }
        else
        {
            var chatModel = ollama.AddModel("chat-model", localModel);
            apiService
                .WithReference(chatModel)
                .WaitFor(chatModel);
        }
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

if (builder.ExecutionContext.IsPublishMode)
{
    web.WithEnvironment("Demo__DashboardUrl", "http://localhost:18888");
}

builder.Build().Run();
