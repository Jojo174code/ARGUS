namespace Argus.Domain.Models;

public sealed record ParsedEmail(
    string? DisplayName,
    string? FromAddress,
    string? ReplyToAddress,
    string? ReturnPath,
    string? Subject,
    DateTimeOffset? Date,
    string? MessageId,
    IReadOnlyList<string> ReceivedHeaders,
    EmailAuthenticationResults Authentication,
    IReadOnlyList<ExtractedUrl> Urls,
    IReadOnlyList<EmailAttachmentMetadata> Attachments,
    string? PlainTextBody,
    string? HtmlBody);