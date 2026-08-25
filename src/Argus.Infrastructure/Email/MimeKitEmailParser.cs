using Argus.Application.Interfaces;
using Argus.Domain.Enums;
using Argus.Domain.Models;
using HtmlAgilityPack;
using MimeKit;
using System.Net;
using System.Text.RegularExpressions;

namespace Argus.Infrastructure.Email;

public sealed partial class MimeKitEmailParser : IEmailParser
{
    [GeneratedRegex(@"https?://[^\s\""""<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AbsoluteUrlRegex();

    [GeneratedRegex(@"\b([a-z0-9-]+\.)+[a-z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BareDomainRegex();

    [GeneratedRegex(@"\b(?<check>spf|dkim|dmarc)\s*=\s*(?<value>pass|fail|softfail|neutral|none|missing)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AuthenticationRegex();

    public async Task<ParsedEmail> ParseAsync(Stream emailStream, CancellationToken cancellationToken)
    {
        var parser = new MimeParser(emailStream, MimeFormat.Entity);
        var message = await parser.ParseMessageAsync(cancellationToken);

        var urls = ExtractUrls(message.TextBody, message.HtmlBody);
        var attachments = ExtractAttachments(message);
        var authentication = ExtractAuthentication(message);

        return new ParsedEmail(
            message.From.Mailboxes.FirstOrDefault()?.Name,
            message.From.Mailboxes.FirstOrDefault()?.Address,
            message.ReplyTo.Mailboxes.FirstOrDefault()?.Address,
            message.Headers[HeaderId.ReturnPath],
            message.Subject,
            message.Date == DateTimeOffset.MinValue ? null : message.Date,
            message.MessageId,
            message.Headers.Where(header => header.Id == HeaderId.Received).Select(header => header.Value).ToList(),
            authentication,
            urls,
            attachments,
            message.TextBody,
            message.HtmlBody);
    }

    private static IReadOnlyList<ExtractedUrl> ExtractUrls(string? plainTextBody, string? htmlBody)
    {
        var extractedUrls = new List<ExtractedUrl>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddUrlsFromText(plainTextBody, "plain-text", extractedUrls, seen, null);

        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlBody);

            var anchors = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            if (anchors is not null)
            {
                foreach (var anchor in anchors)
                {
                    var href = WebUtility.HtmlDecode(anchor.GetAttributeValue("href", string.Empty)).Trim();
                    var displayText = WebUtility.HtmlDecode(anchor.InnerText).Trim();
                    AddUrlCandidate(href, displayText, "html-anchor", extractedUrls, seen);
                }
            }

            AddUrlsFromText(WebUtility.HtmlDecode(htmlDocument.DocumentNode.InnerText), "html-text", extractedUrls, seen, null);
        }

        return extractedUrls;
    }

    private static void AddUrlsFromText(
        string? content,
        string source,
        IList<ExtractedUrl> extractedUrls,
        ISet<string> seen,
        string? displayText)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        foreach (Match match in AbsoluteUrlRegex().Matches(content))
        {
            AddUrlCandidate(match.Value, displayText, source, extractedUrls, seen);
        }
    }

    private static void AddUrlCandidate(
        string candidate,
        string? displayText,
        string source,
        IList<ExtractedUrl> extractedUrls,
        ISet<string> seen)
    {
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var key = $"{uri.AbsoluteUri}|{displayText}|{source}";
        if (seen.Add(key))
        {
            extractedUrls.Add(new ExtractedUrl(uri.AbsoluteUri, string.IsNullOrWhiteSpace(displayText) ? null : displayText, source));
        }
    }

    private static IReadOnlyList<EmailAttachmentMetadata> ExtractAttachments(MimeMessage message)
    {
        return message.Attachments.Select(attachment =>
        {
            return attachment switch
            {
                MimePart mimePart => new EmailAttachmentMetadata(
                    mimePart.FileName ?? "unnamed-attachment",
                    mimePart.ContentType.MimeType,
                    TryGetAttachmentSize(mimePart)),
                MessagePart messagePart => new EmailAttachmentMetadata(
                    messagePart.ContentDisposition?.FileName ?? "attached-message.eml",
                    "message/rfc822",
                    0),
                _ => new EmailAttachmentMetadata("unknown", "application/octet-stream", 0)
            };
        }).ToList();
    }

    private static long TryGetAttachmentSize(MimePart mimePart)
    {
        if (mimePart.ContentDisposition?.Size is long dispositionSize and > 0)
        {
            return dispositionSize;
        }

        return mimePart.Content?.Stream?.CanSeek == true ? mimePart.Content.Stream.Length : 0;
    }

    private static EmailAuthenticationResults ExtractAuthentication(MimeMessage message)
    {
        var authenticationHeaders = message.Headers
            .Where(header => header.Field.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase)
                || header.Field.Equals("Received-SPF", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)
            .ToList();

        var combined = string.Join("; ", authenticationHeaders);
        var verdicts = AuthenticationRegex().Matches(combined)
            .ToDictionary(
                match => match.Groups["check"].Value.ToLowerInvariant(),
                match => ParseVerdict(match.Groups["value"].Value),
                StringComparer.OrdinalIgnoreCase);

        return new EmailAuthenticationResults(
            verdicts.GetValueOrDefault("spf", AuthCheckVerdict.Unknown),
            verdicts.GetValueOrDefault("dkim", AuthCheckVerdict.Missing),
            verdicts.GetValueOrDefault("dmarc", AuthCheckVerdict.Unknown),
            authenticationHeaders);
    }

    private static AuthCheckVerdict ParseVerdict(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pass" => AuthCheckVerdict.Pass,
            "fail" => AuthCheckVerdict.Fail,
            "softfail" => AuthCheckVerdict.SoftFail,
            "neutral" => AuthCheckVerdict.Neutral,
            "none" => AuthCheckVerdict.None,
            "missing" => AuthCheckVerdict.Missing,
            _ => AuthCheckVerdict.Unknown
        };
    }

    public static string? ExtractDisplayDomain(string? displayText)
    {
        if (string.IsNullOrWhiteSpace(displayText))
        {
            return null;
        }

        var trimmed = displayText.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        var match = BareDomainRegex().Match(trimmed);
        return match.Success ? match.Value : null;
    }
}