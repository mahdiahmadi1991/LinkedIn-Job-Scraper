namespace LinkedIn.JobScraper.Web.AI;

public static class OpenAiModelCatalog
{
    private static readonly HashSet<string> SupportedModels = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "gpt-5-mini",
        "gpt-5",
        "gpt-4.1-mini"
    };

    public static IReadOnlyList<OpenAiModelCatalogItem> Items { get; } =
    [
        new(
            "gpt-5-mini",
            "GPT-5 mini",
            "Balanced quality, latency, and cost for recurring job-scoring workloads."),
        new(
            "gpt-5",
            "GPT-5",
            "Higher reasoning quality for stricter ranking and nuanced profile matching."),
        new(
            "gpt-4.1-mini",
            "GPT-4.1 mini",
            "Compatibility fallback with lower cost when GPT-5 family is not required.")
    ];

    public static bool IsSupported(string? model)
    {
        return !string.IsNullOrWhiteSpace(model) && SupportedModels.Contains(model.Trim());
    }
}

public sealed record OpenAiModelCatalogItem(
    string Value,
    string Label,
    string Description);
