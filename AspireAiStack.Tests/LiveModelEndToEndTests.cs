using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using AspireAiStack.ApiService;
using Microsoft.Playwright;

namespace AspireAiStack.Tests;

[Collection(AspireEndToEndCollection.Name)]
public sealed class LiveModelEndToEndTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [LiveModelFact]
    [Trait("Category", "LiveModel")]
    public async Task OllamaQdrantRedisAndBlazor_CompleteFreshAndCachedRequests()
    {
        using var timeout = new CancellationTokenSource(DefaultTimeout);
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireAiStack_AppHost>(
            ["--Demo:UseContainers=true", "--Demo:UseLocalModel=true"],
            timeout.Token);

        await using var app = await appHost.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", timeout.Token);

        var evidenceDirectory = EvidencePaths.Resolve("live-model");
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var apiPrompt = $"Explain how Aspire service references help an AI application. Evidence run {runId}.";

        using var apiClient = app.CreateHttpClient("apiservice", "http");
        var statusJson = await apiClient.GetStringAsync("/api/status", timeout.Token);
        var status = JsonSerializer.Deserialize<StackStatus>(statusJson, JsonOptions)
            ?? throw new InvalidOperationException("The API returned an empty status payload.");

        Assert.Equal("ollama", status.AiMode);
        Assert.Equal("Qdrant", status.VectorStore);
        Assert.Equal("Redis", status.Cache);
        Assert.False(status.SafeDefault);

        var firstExchange = await PostPromptAsync(apiClient, apiPrompt, timeout.Token);
        var secondExchange = await PostPromptAsync(apiClient, apiPrompt, timeout.Token);

        Assert.False(firstExchange.Response.CacheHit);
        Assert.True(secondExchange.Response.CacheHit);
        Assert.Equal("ollama", firstExchange.Response.AiMode);
        Assert.Equal("Qdrant", firstExchange.Response.VectorStore);
        Assert.Equal("Redis", firstExchange.Response.Cache);
        Assert.Equal(3, firstExchange.Response.Sources.Count);
        Assert.Equal(firstExchange.Response.Answer, secondExchange.Response.Answer);
        Assert.DoesNotContain("deterministic response", firstExchange.Response.Answer, StringComparison.OrdinalIgnoreCase);

        var browserEvidence = await ExerciseBrowserAsync(
            app.GetEndpoint("webfrontend", "http"),
            evidenceDirectory,
            runId,
            timeout.Token);

        var evidence = new
        {
            capturedAtUtc = DateTimeOffset.UtcNow,
            runId,
            runtime = RuntimeInformation.FrameworkDescription,
            operatingSystem = RuntimeInformation.OSDescription,
            model = "qwen2.5:3b",
            resources = new
            {
                apiMode = status.AiMode,
                vectorStore = status.VectorStore,
                cache = status.Cache
            },
            http = new
            {
                prompt = apiPrompt,
                firstStatus = (int)firstExchange.StatusCode,
                first = firstExchange.Response,
                secondStatus = (int)secondExchange.StatusCode,
                second = secondExchange.Response
            },
            browser = browserEvidence
        };

        await File.WriteAllTextAsync(
            Path.Combine(evidenceDirectory, "live-model-run.json"),
            JsonSerializer.Serialize(evidence, JsonOptions),
            timeout.Token);
        await File.WriteAllTextAsync(
            Path.Combine(evidenceDirectory, "http-transcript.txt"),
            BuildHttpTranscript(apiPrompt, firstExchange, secondExchange),
            timeout.Token);
    }

    private static async Task<HttpExchange> PostPromptAsync(
        HttpClient client,
        string prompt,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/chat",
            new ChatRequest(prompt),
            cancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.IsSuccessStatusCode,
            $"POST /api/chat returned {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{rawBody}");
        var payload = JsonSerializer.Deserialize<ChatResponse>(rawBody, JsonOptions)
            ?? throw new InvalidOperationException("The API returned an empty chat payload.");
        return new HttpExchange(response.StatusCode, response.Headers.ToString(), rawBody, payload);
    }

    private static async Task<object> ExerciseBrowserAsync(
        Uri webEndpoint,
        string evidenceDirectory,
        string runId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1200 }
        });
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        var tracePath = Path.Combine(evidenceDirectory, "live-model-trace.zip");

        try
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync(
                webEndpoint.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await page.GetByText("ollama", new() { Exact = true }).WaitForAsync(new()
            {
                Timeout = 120_000
            });

            var browserPrompt = $"Why should an Aspire AI application use health checks? Evidence run {runId}.";
            await page.GetByLabel("Question").FillAsync(browserPrompt);
            await page.GetByRole(AriaRole.Button, new() { Name = "Ask the stack" }).ClickAsync();
            await page.GetByText("fresh response", new() { Exact = true }).WaitForAsync(new()
            {
                Timeout = 180_000
            });
            var freshAnswer = await page.Locator(".response-card > p").InnerTextAsync(new LocatorInnerTextOptions
            {
                Timeout = 180_000
            });
            Assert.True(freshAnswer.Length > 40, "Expected a substantive answer from the live model.");
            Assert.Equal(3, await page.Locator(".source-list li").CountAsync());

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(evidenceDirectory, "live-model-ui-fresh.png"),
                FullPage = true
            });

            await page.GetByRole(AriaRole.Button, new() { Name = "Ask the stack" }).ClickAsync();
            await page.GetByText("cache hit", new() { Exact = true }).WaitForAsync(new()
            {
                Timeout = 30_000
            });
            var cachedAnswer = await page.Locator(".response-card > p").InnerTextAsync();
            Assert.Equal(freshAnswer, cachedAnswer);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(evidenceDirectory, "live-model-ui-cache-hit.png"),
                FullPage = true
            });

            return new
            {
                endpoint = webEndpoint,
                prompt = browserPrompt,
                freshAnswer,
                cachedAnswer,
                sourceCount = 3,
                freshScreenshot = "live-model-ui-fresh.png",
                cachedScreenshot = "live-model-ui-cache-hit.png"
            };
        }
        finally
        {
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
        }
    }

    private static string BuildHttpTranscript(
        string prompt,
        HttpExchange first,
        HttpExchange second) =>
        $"""
        POST /api/chat
        Content-Type: application/json

        {JsonSerializer.Serialize(new ChatRequest(prompt), JsonOptions)}

        HTTP {(int)first.StatusCode} {first.StatusCode}
        {first.Headers}
        {first.RawBody}

        POST /api/chat (same prompt; cache verification)
        Content-Type: application/json

        {JsonSerializer.Serialize(new ChatRequest(prompt), JsonOptions)}

        HTTP {(int)second.StatusCode} {second.StatusCode}
        {second.Headers}
        {second.RawBody}
        """;

    private sealed record HttpExchange(
        HttpStatusCode StatusCode,
        string Headers,
        string RawBody,
        ChatResponse Response);
}
