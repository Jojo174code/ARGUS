using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Models;

namespace Argus.Application.Services.InvestigationTools;

public sealed class EmailAuthenticationTool : IInvestigationTool
{
    private static readonly HashSet<string> SupportedTaskTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReviewEmailAuthentication"
    };

    public string Name => "EmailAuthenticationTool";

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
            new("spf", context.Findings.Email.AuthenticationResults.FirstOrDefault(value => value.Contains("spf", StringComparison.OrdinalIgnoreCase)) ?? context.Findings.DeterministicAuthValue("SPF", email), "auth:spf"),
            new("dkim", context.Findings.Email.AuthenticationResults.FirstOrDefault(value => value.Contains("dkim", StringComparison.OrdinalIgnoreCase)) ?? context.Findings.DeterministicAuthValue("DKIM", email), "auth:dkim"),
            new("dmarc", context.Findings.Email.AuthenticationResults.FirstOrDefault(value => value.Contains("dmarc", StringComparison.OrdinalIgnoreCase)) ?? context.Findings.DeterministicAuthValue("DMARC", email), "auth:dmarc")
        };

        var senderMismatchIndicators = context.Findings.Indicators
            .Where(indicator => indicator.RuleId is "SENDER-REPLYTO-001" or "SENDER-RETURNPATH-001" or "SENDER-BRAND-001")
            .Select(indicator => new ToolObservation(
                indicator.RuleId,
                indicator.Evidence ?? indicator.Description,
                $"indicator:{indicator.RuleId}"))
            .ToList();

        observations.AddRange(senderMismatchIndicators);

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

internal static class DeterministicFindingsExtensions
{
    public static string DeterministicAuthValue(this DeterministicFindings findings, string label, EmailSnapshot email)
    {
        var lowerLabel = label.ToLowerInvariant();
        var headerMatch = email.AuthenticationResults
            .FirstOrDefault(value => value.Contains(lowerLabel, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(headerMatch))
        {
            return headerMatch;
        }

        return $"{label}=unknown";
    }
}