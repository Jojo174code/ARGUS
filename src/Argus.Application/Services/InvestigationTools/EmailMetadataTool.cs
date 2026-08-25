using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Models;

namespace Argus.Application.Services.InvestigationTools;

public sealed class EmailMetadataTool : IInvestigationTool
{
    private static readonly HashSet<string> SupportedTaskTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReviewEmailMetadata"
    };

    public string Name => "EmailMetadataTool";

    public bool CanExecute(InvestigationTask task)
    {
        return SupportedTaskTypes.Contains(task.TaskType);
    }

    public Task<ToolResult> ExecuteAsync(InvestigationContext context, InvestigationTask task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = context.Findings.Email;
        var observations = new List<ToolObservation>
        {
            new("fromAddress", email.FromAddress ?? "unknown", "email:from"),
            new("replyToAddress", email.ReplyToAddress ?? "none", "email:reply-to"),
            new("returnPath", email.ReturnPath ?? "none", "email:return-path"),
            new("subject", email.Subject ?? "none", "email:subject"),
            new("messageId", email.MessageId ?? "none", "email:message-id"),
            new("date", email.Date ?? "none", "email:date"),
            new("authenticationHeaders", string.Join(" | ", email.AuthenticationResults), "email:auth-headers"),
            new("attachmentCount", email.Attachments.Count.ToString(), "email:attachments")
        };

        var attachmentObservations = email.Attachments
            .Select((attachment, index) => new ToolObservation(
                $"attachment[{index}]",
                $"{attachment.FileName} ({attachment.ContentType}, {attachment.Size} bytes)",
                $"attachment:{attachment.FileName}"))
            .ToList();

        observations.AddRange(attachmentObservations);

        var evidenceReferences = observations
            .Select(observation => observation.EvidenceReference)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new ToolResult(
            Name,
            true,
            observations,
            evidenceReferences,
            null));
    }
}