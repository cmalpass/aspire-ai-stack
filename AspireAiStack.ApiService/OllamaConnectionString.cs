using System.Data.Common;

namespace AspireAiStack.ApiService;

public static class OllamaConnectionString
{
    public static Uri ResolveEndpoint(string connectionString)
    {
        if (TryCreateHttpUri(connectionString, out var endpoint))
        {
            return endpoint;
        }

        try
        {
            var values = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (values.TryGetValue("Endpoint", out var configuredEndpoint)
                && TryCreateHttpUri(configuredEndpoint?.ToString(), out endpoint))
            {
                return endpoint;
            }
        }
        catch (ArgumentException)
        {
            // Fall through to the stable, domain-specific configuration error below.
        }

        throw new InvalidOperationException(
            "The Ollama connection string must be an HTTP(S) URI or contain a valid Endpoint value.");
    }

    private static bool TryCreateHttpUri(string? value, out Uri endpoint)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            endpoint = candidate;
            return true;
        }

        endpoint = null!;
        return false;
    }
}
