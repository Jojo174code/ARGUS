using Argus.Domain.Enums;
using Argus.Infrastructure.Email;

namespace Argus.UnitTests.Email;

public sealed class MimeKitEmailParserTests
{
    private readonly MimeKitEmailParser _parser = new();

    [Fact]
    public async Task ParseAsync_StandardEmail_ExtractsExpectedFields()
    {
        await using var stream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-microsoft-login.eml"));

        var parsed = await _parser.ParseAsync(stream, CancellationToken.None);

        Assert.Equal("Microsoft Security", parsed.DisplayName);
        Assert.Equal("notice@alerts-example.net", parsed.FromAddress);
        Assert.Equal("support@identity-help.example", parsed.ReplyToAddress);
        Assert.Equal("<bounce@mailer-host.example>", parsed.ReturnPath);
        Assert.Equal("Urgent: verify your login to avoid account suspension", parsed.Subject);
        Assert.Equal(AuthCheckVerdict.Fail, parsed.Authentication.Spf);
        Assert.Equal(AuthCheckVerdict.Missing, parsed.Authentication.Dkim);
        Assert.Equal(AuthCheckVerdict.Fail, parsed.Authentication.Dmarc);
        Assert.Single(parsed.ReceivedHeaders);
    }

    [Fact]
    public async Task ParseAsync_MissingReplyTo_ReturnsNullReplyToAddress()
    {
        await using var stream = File.OpenRead(TestFileHelper.GetSampleEmailPath("benign-business-email.eml"));

        var parsed = await _parser.ParseAsync(stream, CancellationToken.None);

        Assert.Null(parsed.ReplyToAddress);
        Assert.Equal("dana@school.example", parsed.FromAddress);
    }

    [Fact]
    public async Task ParseAsync_HtmlEmail_ExtractsAnchorTextAndHref()
    {
        await using var stream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-urgent-password-reset.eml"));

        var parsed = await _parser.ParseAsync(stream, CancellationToken.None);

        var anchorUrl = Assert.Single(parsed.Urls, url => url.Source == "html-anchor");
        Assert.Equal("https://accounts-school.example.com/login", anchorUrl.Url);
        Assert.Equal("portal.school.example", anchorUrl.DisplayText);
    }

    [Fact]
    public async Task ParseAsync_PlainTextEmail_ExtractsMultipleUrls()
    {
        await using var stream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-fake-invoice.eml"));

        var parsed = await _parser.ParseAsync(stream, CancellationToken.None);

        Assert.Contains(parsed.Urls, url => url.Url == "http://203.0.113.24/pay");
        Assert.Contains(parsed.Urls, url => url.Url == "https://xn--paymnt-2va.example/pay");
    }

    [Fact]
    public async Task ParseAsync_Attachments_ExtractsMetadata()
    {
        await using var stream = File.OpenRead(TestFileHelper.GetSampleEmailPath("phishing-fake-invoice.eml"));

        var parsed = await _parser.ParseAsync(stream, CancellationToken.None);

        var attachment = Assert.Single(parsed.Attachments);
        Assert.Equal("invoice.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
    }
}