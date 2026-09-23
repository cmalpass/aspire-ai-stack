using Microsoft.Playwright;

namespace AspireAiStack.Tests;

[Collection(AspireEndToEndCollection.Name)]
public sealed class BrowserSmokeTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task CredentialFreeFlow_RendersAndCompletesInChromium()
    {
        using var timeout = new CancellationTokenSource(DefaultTimeout);
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireAiStack_AppHost>(
            ["--Demo:UseContainers=false"],
            timeout.Token);

        await using var app = await appHost.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", timeout.Token);

        var evidenceDirectory = EvidencePaths.Resolve("browser-smoke");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1100 }
        });
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        try
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync(
                app.GetEndpoint("webfrontend", "http").ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await page.GetByRole(AriaRole.Heading, new()
            {
                Name = "Orchestrate the whole AI stack without hiding its boundaries."
            }).WaitForAsync();
            await page.GetByText("simulated", new() { Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Heading, new()
            {
                Name = "What you should see"
            }).WaitForAsync();
            await page.GetByText("fresh response", new() { Exact = true }).WaitForAsync();
            await page.GetByText("cache hit", new() { Exact = true }).WaitForAsync();

            var vectorLabel = page.Locator(".status-card dt").Filter(new() { HasText = "Vectors" });
            var vectorValue = page.Locator(".status-card dd").Filter(new()
            {
                HasText = "in-memory deterministic vectors"
            });
            var labelBox = await vectorLabel.BoundingBoxAsync();
            var valueBox = await vectorValue.BoundingBoxAsync();
            Assert.NotNull(labelBox);
            Assert.NotNull(valueBox);
            Assert.True(
                valueBox.X >= labelBox.X + labelBox.Width + 8,
                "The vector-store value should not crowd or overlap its label.");

            await page.GetByLabel("Question").FillAsync("How does Aspire connect the API to Qdrant?");
            await page.GetByRole(AriaRole.Button, new() { Name = "Ask the stack" }).ClickAsync();
            await page.Locator(".response-card").WaitForAsync();

            var responseText = await page.Locator(".response-card > p").InnerTextAsync();
            Assert.Contains("Aspire", responseText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(3, await page.Locator(".source-list li").CountAsync());

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(evidenceDirectory, "browser-smoke.png"),
                FullPage = true
            });
        }
        finally
        {
            await context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(evidenceDirectory, "browser-smoke-trace.zip")
            });
        }
    }
}
