using AspireAiStack.ApiService;

namespace AspireAiStack.Tests;

public sealed class OllamaConnectionStringTests
{
    [Theory]
    [InlineData("http://localhost:11434", "http://localhost:11434/")]
    [InlineData("Endpoint=http://localhost:11434", "http://localhost:11434/")]
    [InlineData("Endpoint=https://ollama.internal:443;Ignored=value", "https://ollama.internal/")]
    public void ResolveEndpoint_AcceptsRawAndAspireConnectionStrings(string value, string expected)
    {
        Assert.Equal(new Uri(expected), OllamaConnectionString.ResolveEndpoint(value));
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("Endpoint=ftp://localhost/model")]
    [InlineData("Host=localhost")]
    public void ResolveEndpoint_RejectsInvalidValues(string value)
    {
        Assert.Throws<InvalidOperationException>(() => OllamaConnectionString.ResolveEndpoint(value));
    }
}
