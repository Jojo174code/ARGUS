namespace Argus.Domain.Models;

public sealed record WarningSign(
    string Title,
    string Explanation,
    IReadOnlyList<string> SupportingFindingIds);