using System.Text.Json;
using Microsoft.Playwright;

namespace AspireAiStack.Tests;

[Collection(AspireEndToEndCollection.Name)]
public sealed class ComposeBrowserEvidenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [ComposeFact]
    [Trait("Category", "ComposeEvidence")]
    public async Task ComposeStack_CompletesFreshAndCachedBrowserRequests()
    {
        var endpoint = new Uri(
            Environment.GetEnvironmentVariable("COMPOSE_BASE_URL")!,
            UriKind.Absolute);
        var evidenceDirectory = EvidencePaths.Resolve("compose");
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");

        using var httpClient = new HttpClient { BaseAddress = endpoint };
        using var healthResponse = await WaitForHealthAsync(httpClient, endpoint);
        Assert.True(
            healthResponse.IsSuccessStatusCode,
            $"The Compose web container health endpoint returned {(int)healthResponse.StatusCode}.");

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

        var tracePath = Path.Combine(evidenceDirectory, "compose-trace.zip");

        try
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync(
                endpoint.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 120_000 });
            await page.GetByText("ollama", new() { Exact = true }).WaitForAsync(new()
            {
                Timeout = 120_000
            });
            var dashboardLink = page.GetByRole(AriaRole.Link, new() { Name = "Open the Aspire dashboard ↗" });
            Assert.Equal("http://localhost:18888", await dashboardLink.GetAttributeAsync("href"));

            var prompt = $"How does one Compose file make an AI application easier to inspect? Evidence run {runId}.";
            await page.GetByLabel("Question").FillAsync(prompt);
            await page.GetByRole(AriaRole.Button, new() { Name = "Ask the stack" }).ClickAsync();
            await page.GetByText("fresh response", new() { Exact = true }).WaitForAsync(new()
            {
                Timeout = 180_000
            });

            var freshAnswer = await page.Locator(".response-card > p").InnerTextAsync();
            var sourceCount = await page.Locator(".source-list li").CountAsync();
            Assert.True(freshAnswer.Length > 40, "Expected a substantive answer from the live model.");
            Assert.Equal(3, sourceCount);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(evidenceDirectory, "compose-ui-fresh.png"),
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
                Path = Path.Combine(evidenceDirectory, "compose-ui-cache-hit.png"),
                FullPage = true
            });

            var evidence = new
            {
                capturedAtUtc = DateTimeOffset.UtcNow,
                endpoint,
                health = new
                {
                    statusCode = (int)healthResponse.StatusCode,
                    status = healthResponse.StatusCode.ToString()
                },
                dashboard = new
                {
                    url = "http://localhost:18888",
                    homepageLinkVerified = true
                },
                prompt,
                fresh = new
                {
                    answer = freshAnswer,
                    sourceCount,
                    cacheHit = false,
                    screenshot = "compose-ui-fresh.png"
                },
                cached = new
                {
                    answer = cachedAnswer,
                    sourceCount,
                    cacheHit = true,
                    screenshot = "compose-ui-cache-hit.png"
                },
                trace = "compose-trace.zip"
            };

            await File.WriteAllTextAsync(
                Path.Combine(evidenceDirectory, "compose-run.json"),
                JsonSerializer.Serialize(evidence, JsonOptions));
        }
        finally
        {
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
        }
    }

    private static async Task<HttpResponseMessage> WaitForHealthAsync(
        HttpClient client,
        Uri endpoint)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(new Uri(endpoint, "/health"));
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException exception)
            {
                lastError = exception;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException(
            $"The Compose web container did not become healthy before the timeout. {lastError?.Message}",
            lastError);
    }
}
