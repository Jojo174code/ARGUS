using Argus.Domain.Enums;

namespace Argus.Domain.Models;

public sealed record EmailAuthenticationResults(
    AuthCheckVerdict Spf,
    AuthCheckVerdict Dkim,
    AuthCheckVerdict Dmarc,
    IReadOnlyList<string> AuthenticationResultsHeaders);