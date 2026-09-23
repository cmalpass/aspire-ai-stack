namespace AspireAiStack.Tests;

internal static class EvidencePaths
{
    public static string Resolve(string defaultName)
    {
        var configuredPath = Environment.GetEnvironmentVariable("EVIDENCE_OUTPUT_DIR");
        var outputPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "TestResults", defaultName)
            : Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(outputPath);
        return outputPath;
    }
}
