namespace Argus.Domain.Models;

public sealed record ExtractedUrl(
    string Url,
    string? DisplayText,
    string Source);