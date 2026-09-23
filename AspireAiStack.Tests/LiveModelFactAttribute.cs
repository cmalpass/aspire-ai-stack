namespace AspireAiStack.Tests;

public sealed class LiveModelFactAttribute : FactAttribute
{
    public LiveModelFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_LIVE_MODEL_E2E"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set RUN_LIVE_MODEL_E2E=true to run the containerized Ollama evidence flow.";
        }
    }
}
