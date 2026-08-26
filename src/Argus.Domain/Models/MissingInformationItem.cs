namespace Argus.Domain.Models;

public sealed record MissingInformationItem(
    string Question,
    string Reason,
    bool Required,
    bool BlocksInvestigation = false);