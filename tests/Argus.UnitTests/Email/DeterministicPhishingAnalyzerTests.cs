using Argus.Domain.Enums;
using Argus.Domain.Models;
using Argus.Infrastructure.Configuration;
using Argus.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace Argus.UnitTests.Email;

public sealed class DeterministicPhishingAnalyzerTests
{
    private readonly DeterministicPhishingAnalyzer _analyzer;

    public DeterministicPhishingAnalyzerTests()
    {
        var options = Options.Create(new PhishingAnalysisOptions
        {
            MaxHostnameLength = 40,
            MaxSubdomainCount = 3,
            KnownBrands =
            [
                new KnownBrandOption { Name = "Microsoft", AllowedDomains = ["microsoft.com", "microsoftonline.com", "office.com", "outlook.com"] },
                new KnownBrandOption { Name = "Google", AllowedDomains = ["google.com", "gmail.com"] },
                new KnownBrandOption { Name = "Apple", AllowedDomains = ["apple.com", "icloud.com"] },
                new KnownBrandOption { Name = "Amazon", AllowedDomains = ["amazon.com", "amazonaws.com"] },
                new KnownBrandOption { Name = "PayPal", AllowedDomains = ["paypal.com"] }
            ]
        });

        _analyzer = new DeterministicPhishingAnalyzer(options, new StaticMitreAttackMapper());
    }

    [Fact]
    public async Task AnalyzeAsync_SpfFail_TriggersSpfFailureRule()
    {
        var email = CreateEmail(authentication: new EmailAuthenticationResults(AuthCheckVerdict.Fail, AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, []));

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.RuleResults.Single(rule => rule.RuleId == "EMAIL-AUTH-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_SpfPass_DoesNotTriggerSpfFailureRule()
    {
        var email = CreateEmail(authentication: new EmailAuthenticationResults(AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, []));

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.RuleResults.Single(rule => rule.RuleId == "EMAIL-AUTH-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_ReplyToMismatch_TriggersRule()
    {
        var email = CreateEmail(fromAddress: "sender@school.example", replyToAddress: "support@other.example");

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.RuleResults.Single(rule => rule.RuleId == "SENDER-REPLYTO-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_SameDomainReplyTo_DoesNotTriggerRule()
    {
        var email = CreateEmail(fromAddress: "sender@school.example", replyToAddress: "support@school.example");

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.RuleResults.Single(rule => rule.RuleId == "SENDER-REPLYTO-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_IpAddressUrl_TriggersRule()
    {
        var email = CreateEmail(urls: [new ExtractedUrl("http://203.0.113.24/pay", null, "plain-text")]);

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.RuleResults.Single(rule => rule.RuleId == "URL-IP-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_PunycodeUrl_TriggersRule()
    {
        var email = CreateEmail(urls: [new ExtractedUrl("https://xn--paymnt-2va.example/pay", null, "plain-text")]);

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.RuleResults.Single(rule => rule.RuleId == "URL-PUNYCODE-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_DisplayedUrlMismatch_TriggersRule()
    {
        var email = CreateEmail(urls: [new ExtractedUrl("https://accounts-school.example.com/login", "portal.school.example", "html-anchor")]);

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.RuleResults.Single(rule => rule.RuleId == "URL-DISPLAY-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_LegitimateMicrosoftHostname_DoesNotTriggerBrandHostnameRule()
    {
        var email = CreateEmail(urls: [new ExtractedUrl("https://login.microsoftonline.com", "https://login.microsoftonline.com", "html-anchor")]);

        var result = await _analyzer.AnalyzeAsync(email, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.RuleResults.Single(rule => rule.RuleId == "URL-BRAND-001").Triggered);
    }

    [Fact]
    public async Task AnalyzeAsync_IsDeterministicAndBounded()
    {
        var email = CreateEmail(
            displayName: "Microsoft Security",
            fromAddress: "notice@alerts-example.net",
            replyToAddress: "support@identity-help.example",
            returnPath: "<bounce@mailer-host.example>",
            subject: "Urgent verify your login",
            urls:
            [
                new ExtractedUrl("https://login-microsoft.secure-access.example.net/session", "https://login.microsoftonline.com", "html-anchor"),
                new ExtractedUrl("http://203.0.113.24/pay", null, "plain-text")
            ],
            authentication: new EmailAuthenticationResults(AuthCheckVerdict.Fail, AuthCheckVerdict.Missing, AuthCheckVerdict.Fail, []),
            plainTextBody: "Urgent action required. Verify your login immediately and reset your password.");

        var first = await _analyzer.AnalyzeAsync(email, Guid.Parse("11111111-1111-1111-1111-111111111111"), CancellationToken.None);
        var second = await _analyzer.AnalyzeAsync(email, Guid.Parse("11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        Assert.Equal(first.RiskScore, second.RiskScore);
        Assert.InRange(first.RiskScore, 0, 100);
    }

    [Fact]
    public async Task AnalyzeAsync_RiskLevelThresholds_MapCorrectly()
    {
        var low = await _analyzer.AnalyzeAsync(CreateEmail(authentication: new EmailAuthenticationResults(AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, [])), Guid.NewGuid(), CancellationToken.None);
        var moderate = await _analyzer.AnalyzeAsync(CreateEmail(replyToAddress: "other@alerts.example", plainTextBody: "Urgent action required.", authentication: new EmailAuthenticationResults(AuthCheckVerdict.Pass, AuthCheckVerdict.Missing, AuthCheckVerdict.Pass, [])), Guid.NewGuid(), CancellationToken.None);
        var high = await _analyzer.AnalyzeAsync(CreateEmail(replyToAddress: "other@alerts.example", urls: [new ExtractedUrl("https://accounts-school.example.com/login", "portal.school.example", "html-anchor")], authentication: new EmailAuthenticationResults(AuthCheckVerdict.Fail, AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, [])), Guid.NewGuid(), CancellationToken.None);
        var critical = await _analyzer.AnalyzeAsync(CreateEmail(displayName: "Microsoft Security", fromAddress: "notice@alerts-example.net", replyToAddress: "support@identity-help.example", returnPath: "<bounce@mailer-host.example>", subject: "Urgent verify your login", plainTextBody: "Urgent action required. Verify your login immediately and reset your password.", urls: [new ExtractedUrl("https://login-microsoft.secure-access.example.net/session", "https://login.microsoftonline.com", "html-anchor")], authentication: new EmailAuthenticationResults(AuthCheckVerdict.Fail, AuthCheckVerdict.Missing, AuthCheckVerdict.Fail, [])), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(RiskLevel.Low, low.RiskLevel);
        Assert.Equal(RiskLevel.Moderate, moderate.RiskLevel);
        Assert.Equal(RiskLevel.High, high.RiskLevel);
        Assert.Equal(RiskLevel.Critical, critical.RiskLevel);
    }

    private static ParsedEmail CreateEmail(
        string? displayName = "Sender",
        string? fromAddress = "sender@school.example",
        string? replyToAddress = null,
        string? returnPath = "<sender@school.example>",
        string? subject = "Status update",
        IReadOnlyList<ExtractedUrl>? urls = null,
        EmailAuthenticationResults? authentication = null,
        string? plainTextBody = "Hello world",
        string? htmlBody = null)
    {
        return new ParsedEmail(
            displayName,
            fromAddress,
            replyToAddress,
            returnPath,
            subject,
            DateTimeOffset.UtcNow,
            "<message-id@example>",
            ["from mail.school.example by mx.recipient.example"],
            authentication ?? new EmailAuthenticationResults(AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, AuthCheckVerdict.Pass, []),
            urls ?? Array.Empty<ExtractedUrl>(),
            Array.Empty<EmailAttachmentMetadata>(),
            plainTextBody,
            htmlBody);
    }
}