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
