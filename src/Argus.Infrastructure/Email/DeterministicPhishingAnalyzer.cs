using Argus.Application.Interfaces;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Net;

namespace Argus.Infrastructure.Email;

public sealed class DeterministicPhishingAnalyzer : IPhishingAnalyzer
{
    private readonly IReadOnlyList<KnownBrandOption> _knownBrands;
    private readonly int _maxHostnameLength;
    private readonly int _maxSubdomainCount;
    private readonly IMitreAttackMapper _mitreAttackMapper;

    public DeterministicPhishingAnalyzer(IOptions<PhishingAnalysisOptions> options, IMitreAttackMapper mitreAttackMapper)
    {
        var value = options.Value;
        _knownBrands = value.KnownBrands;
        _maxHostnameLength = value.MaxHostnameLength;
        _maxSubdomainCount = value.MaxSubdomainCount;
        _mitreAttackMapper = mitreAttackMapper;
    }

    public Task<PhishingAnalysisResult> AnalyzeAsync(ParsedEmail email, Guid incidentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<PhishingRuleResult>();
        EvaluateSenderIdentity(email, results);
        EvaluateAuthentication(email, results);
        EvaluateUrls(email, results);
        EvaluateSocialEngineering(email, results);

        var riskScore = Math.Clamp(results.Where(rule => rule.Triggered).Sum(rule => rule.ScoreContribution), 0, 100);
        var riskLevel = DetermineRiskLevel(riskScore);
        var indicators = results.Where(rule => rule.Triggered).ToList();
        var mappings = _mitreAttackMapper.Map(email, results);
        var summary = indicators.Count == 0
            ? "No high-confidence phishing indicators were triggered by the current deterministic rules."
            : $"{indicators.Count} suspicious indicator(s) were detected across sender identity, authentication, URL, or social-engineering heuristics.";

        return Task.FromResult(new PhishingAnalysisResult(
            incidentId,
            email,
            riskScore,
            riskLevel,
            summary,
            results,
            indicators,
            mappings,
            DateTimeOffset.UtcNow));
    }

    private void EvaluateSenderIdentity(ParsedEmail email, IList<PhishingRuleResult> results)
    {
        var fromDomain = GetDomainFromEmailAddress(email.FromAddress);
        var replyToDomain = GetDomainFromEmailAddress(email.ReplyToAddress);
        var returnPathDomain = GetDomainFromEmailAddress(email.ReturnPath);

        var replyMismatch = !string.IsNullOrWhiteSpace(fromDomain)
            && !string.IsNullOrWhiteSpace(replyToDomain)
            && !DomainsEquivalent(fromDomain, replyToDomain);

        results.Add(new PhishingRuleResult(
            "SENDER-REPLYTO-001",
            "Reply-To Domain Mismatch",
            "Reply-To points to a different domain than the From address.",
            RuleSeverity.Moderate,
            15,
            replyMismatch ? $"from={fromDomain}; reply-to={replyToDomain}" : null,
            replyMismatch));

        var returnPathMismatch = !string.IsNullOrWhiteSpace(fromDomain)
            && !string.IsNullOrWhiteSpace(returnPathDomain)
            && !DomainsEquivalent(fromDomain, returnPathDomain);

        results.Add(new PhishingRuleResult(
            "SENDER-RETURNPATH-001",
            "Return-Path Domain Mismatch",
            "Return-Path differs from the From address domain.",
            RuleSeverity.Moderate,
            10,
            returnPathMismatch ? $"from={fromDomain}; return-path={returnPathDomain}" : null,
            returnPathMismatch));

        var brandRule = EvaluateBrandImpersonation(email.DisplayName, fromDomain);
        results.Add(brandRule);
    }

    private void EvaluateAuthentication(ParsedEmail email, IList<PhishingRuleResult> results)
    {
        results.Add(new PhishingRuleResult(
            "EMAIL-AUTH-001",
            "SPF Failure",
            "SPF authentication failed.",
            RuleSeverity.High,
            20,
            email.Authentication.Spf == AuthCheckVerdict.Fail ? "spf=fail" : null,
            email.Authentication.Spf == AuthCheckVerdict.Fail));

        results.Add(new PhishingRuleResult(
            "EMAIL-AUTH-002",
            "DKIM Failure",
            "DKIM authentication failed.",
            RuleSeverity.High,
            15,
            email.Authentication.Dkim == AuthCheckVerdict.Fail ? "dkim=fail" : null,
            email.Authentication.Dkim == AuthCheckVerdict.Fail));

        results.Add(new PhishingRuleResult(
            "EMAIL-AUTH-003",
            "DKIM Missing",
            "DKIM was not present in the observed authentication headers.",
            RuleSeverity.Low,
            8,
            email.Authentication.Dkim == AuthCheckVerdict.Missing ? "dkim=missing" : null,
            email.Authentication.Dkim == AuthCheckVerdict.Missing));

        results.Add(new PhishingRuleResult(
            "EMAIL-AUTH-004",
            "DMARC Failure",
            "DMARC authentication failed.",
            RuleSeverity.High,
            15,
            email.Authentication.Dmarc == AuthCheckVerdict.Fail ? "dmarc=fail" : null,
            email.Authentication.Dmarc == AuthCheckVerdict.Fail));
    }

    private void EvaluateUrls(ParsedEmail email, IList<PhishingRuleResult> results)
    {
        var ipUrls = email.Urls.Where(url => Uri.TryCreate(url.Url, UriKind.Absolute, out var uri) && IPAddress.TryParse(uri.Host, out _)).ToList();
        results.Add(new PhishingRuleResult(
            "URL-IP-001",
            "Link Uses IP Address",
            "A URL uses an IP address in place of a hostname.",
            RuleSeverity.High,
            20,
            ipUrls.FirstOrDefault()?.Url,
            ipUrls.Count > 0));

        var punycodeUrls = email.Urls.Where(url => Uri.TryCreate(url.Url, UriKind.Absolute, out var uri) && uri.Host.Contains("xn--", StringComparison.OrdinalIgnoreCase)).ToList();
        results.Add(new PhishingRuleResult(
            "URL-PUNYCODE-001",
            "Punycode Hostname",
            "A URL contains a punycode hostname that may mask the rendered domain.",
            RuleSeverity.High,
            18,
            punycodeUrls.FirstOrDefault()?.Url,
            punycodeUrls.Count > 0));

        var excessiveSubdomains = email.Urls.Where(url => Uri.TryCreate(url.Url, UriKind.Absolute, out var uri) && GetSubdomainCount(uri.Host) > _maxSubdomainCount).ToList();
        results.Add(new PhishingRuleResult(
            "URL-SUBDOMAIN-001",
            "Excessive Subdomains",
            "A URL hostname contains more subdomains than expected for a typical service domain.",
            RuleSeverity.Moderate,
            10,
            excessiveSubdomains.FirstOrDefault()?.Url,
            excessiveSubdomains.Count > 0));

        var longHostnames = email.Urls.Where(url => Uri.TryCreate(url.Url, UriKind.Absolute, out var uri) && uri.Host.Length > _maxHostnameLength).ToList();
        results.Add(new PhishingRuleResult(
            "URL-LENGTH-001",
            "Suspiciously Long Hostname",
            "A URL hostname length exceeds the configured heuristic threshold.",
            RuleSeverity.Low,
            8,
            longHostnames.FirstOrDefault()?.Url,
            longHostnames.Count > 0));

        var displayMismatch = email.Urls
            .Select(url => new
            {
                Url = url,
                DisplayDomain = MimeKitEmailParser.ExtractDisplayDomain(url.DisplayText)
            })
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.DisplayDomain)
                && Uri.TryCreate(candidate.Url.Url, UriKind.Absolute, out var actualUri)
                && !DomainsEquivalent(candidate.DisplayDomain!, actualUri.Host));

        results.Add(new PhishingRuleResult(
            "URL-DISPLAY-001",
            "Displayed Link Mismatch",
            "The visible link text points to a different domain than the actual destination.",
            RuleSeverity.High,
            20,
            displayMismatch is null ? null : $"display={displayMismatch.DisplayDomain}; actual={new Uri(displayMismatch.Url.Url).Host}",
            displayMismatch is not null));

        var brandHostnameMismatch = email.Urls
            .Select(url => new
            {
                Url = url.Url,
                Host = Uri.TryCreate(url.Url, UriKind.Absolute, out var uri) ? uri.Host : null,
                Brand = FindImpersonatedBrand(Uri.TryCreate(url.Url, UriKind.Absolute, out uri) ? uri.Host : null, _knownBrands)
            })
            .FirstOrDefault(candidate => candidate.Brand is not null && candidate.Host is not null);

        results.Add(new PhishingRuleResult(
            "URL-BRAND-001",
            "Brand Name in Unrelated Hostname",
            "A hostname contains a known brand token but does not belong to the brand's known domains.",
            RuleSeverity.High,
            18,
            brandHostnameMismatch is null ? null : $"brand={brandHostnameMismatch.Brand!.Name}; host={brandHostnameMismatch.Host}",
            brandHostnameMismatch is not null));
    }

    private static void EvaluateSocialEngineering(ParsedEmail email, IList<PhishingRuleResult> results)
    {
        var content = string.Join('\n', new[] { email.Subject, email.PlainTextBody, email.HtmlBody }.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();

        AddKeywordRule(results, "SOCIAL-URGENCY-001", "Urgency Language", "The message uses urgency cues that pressure the recipient.", RuleSeverity.Low, 8, content, ["urgent", "immediately", "action required", "asap"]);
        AddKeywordRule(results, "SOCIAL-ACCOUNT-001", "Account Suspension Language", "The message references an account being suspended or disabled.", RuleSeverity.Moderate, 10, content, ["account suspended", "account disabled", "mailbox disabled"]);
        AddKeywordRule(results, "SOCIAL-PASSWORD-001", "Password Reset Theme", "The message prompts for a password reset or change.", RuleSeverity.Moderate, 10, content, ["password reset", "reset your password", "change your password"]);
        AddKeywordRule(results, "SOCIAL-PAYMENT-001", "Payment Request Theme", "The message requests a transfer or immediate payment action.", RuleSeverity.Moderate, 12, content, ["payment", "wire transfer", "bank transfer"]);
        AddKeywordRule(results, "SOCIAL-INVOICE-001", "Invoice Theme", "The message references an invoice or billing document.", RuleSeverity.Moderate, 10, content, ["invoice", "billing", "statement due"]);
        AddKeywordRule(results, "SOCIAL-GIFTCARD-001", "Gift Card Theme", "The message references gift cards or voucher purchases.", RuleSeverity.High, 14, content, ["gift card", "itunes card", "voucher"]);
        AddKeywordRule(results, "SOCIAL-LOGIN-001", "Login Verification Theme", "The message instructs the recipient to verify or confirm a login.", RuleSeverity.Moderate, 10, content, ["verify your login", "login verification", "confirm your account"]);
    }

    private static void AddKeywordRule(
        IList<PhishingRuleResult> results,
        string ruleId,
        string name,
        string description,
        RuleSeverity severity,
        int scoreContribution,
        string content,
        IReadOnlyList<string> keywords)
    {
        var matches = keywords.Where(content.Contains).ToList();
        results.Add(new PhishingRuleResult(
            ruleId,
            name,
            description,
            severity,
            scoreContribution,
            matches.Count == 0 ? null : string.Join(", ", matches),
            matches.Count > 0));
    }

    private PhishingRuleResult EvaluateBrandImpersonation(string? displayName, string? fromDomain)
    {
        var brand = _knownBrands.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(displayName)
            && displayName.Contains(candidate.Name, StringComparison.OrdinalIgnoreCase));

        var triggered = brand is not null
            && !string.IsNullOrWhiteSpace(fromDomain)
            && !brand.AllowedDomains.Any(allowed => DomainMatchesAllowed(fromDomain, allowed));

        return new PhishingRuleResult(
            "SENDER-BRAND-001",
            "Display Name Brand Impersonation",
            "The display name references a known brand, but the sender domain does not align to that brand.",
            RuleSeverity.High,
            20,
            triggered ? $"brand={brand!.Name}; from={fromDomain}" : null,
            triggered);
    }

    private static KnownBrandOption? FindImpersonatedBrand(string? host, IReadOnlyList<KnownBrandOption> knownBrands)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        foreach (var brand in knownBrands)
        {
            var normalizedBrand = NormalizeToken(brand.Name);
            var tokens = host.Split(['.', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeToken)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!tokens.Contains(normalizedBrand))
            {
                continue;
            }

            if (!brand.AllowedDomains.Any(allowed => DomainMatchesAllowed(host, allowed)))
            {
                return brand;
            }
        }

        return null;
    }

    private static RiskLevel DetermineRiskLevel(int riskScore)
    {
        return riskScore switch
        {
            >= 75 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 25 => RiskLevel.Moderate,
            _ => RiskLevel.Low
        };
    }

    private static string? GetDomainFromEmailAddress(string? emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return null;
        }

        var trimmed = emailAddress.Trim().Trim('<', '>');
        var separatorIndex = trimmed.LastIndexOf('@');
        return separatorIndex >= 0 && separatorIndex < trimmed.Length - 1
            ? trimmed[(separatorIndex + 1)..].ToLowerInvariant()
            : null;
    }

    private static int GetSubdomainCount(string host)
    {
        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return Math.Max(0, labels.Length - 2);
    }

    private static bool DomainMatchesAllowed(string actualDomain, string allowedDomain)
    {
        return actualDomain.Equals(allowedDomain, StringComparison.OrdinalIgnoreCase)
            || actualDomain.EndsWith($".{allowedDomain}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DomainsEquivalent(string left, string right)
    {
        return NormalizeComparableDomain(left) == NormalizeComparableDomain(right);
    }

    private static string NormalizeComparableDomain(string domain)
    {
        var labels = domain.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return labels.Length <= 2 ? domain.ToLowerInvariant() : string.Join('.', labels[^2], labels[^1]).ToLowerInvariant();
    }

    private static string NormalizeToken(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}