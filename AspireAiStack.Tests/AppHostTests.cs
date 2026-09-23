using Microsoft.Extensions.Logging;

namespace AspireAiStack.Tests;

public sealed class AppHostTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task CredentialFreeApplicationModel_StartsApiAndWeb()
    {
        using var timeout = new CancellationTokenSource(DefaultTimeout);
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireAiStack_AppHost>(
            ["--Demo:UseContainers=false"],
            timeout.Token);
        appHost.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        await using var app = await appHost.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);

        await app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", timeout.Token);

        using var apiClient = app.CreateHttpClient("apiservice");
        using var webClient = app.CreateHttpClient("webfrontend");
        using var apiResponse = await apiClient.GetAsync("/api/status", timeout.Token);
        using var webResponse = await webClient.GetAsync("/", timeout.Token);

        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, webResponse.StatusCode);
        Assert.Contains("Orchestrate the whole AI stack", await webResponse.Content.ReadAsStringAsync(timeout.Token));
    }
}
