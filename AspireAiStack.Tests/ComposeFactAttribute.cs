namespace AspireAiStack.Tests;

public sealed class ComposeFactAttribute : FactAttribute
{
    public ComposeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COMPOSE_BASE_URL")))
        {
            Skip = "Set COMPOSE_BASE_URL to run the deployed Docker Compose evidence flow.";
        }
    }
}
