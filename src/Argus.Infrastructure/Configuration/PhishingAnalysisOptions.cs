namespace Argus.Infrastructure.Configuration;

public sealed class PhishingAnalysisOptions
{
    public const string SectionName = "PhishingAnalysis";

    public List<KnownBrandOption> KnownBrands { get; init; } = [];

    public int MaxHostnameLength { get; init; } = 40;

    public int MaxSubdomainCount { get; init; } = 3;
}

public sealed class KnownBrandOption
{
    public string Name { get; init; } = string.Empty;

    public List<string> AllowedDomains { get; init; } = [];
}