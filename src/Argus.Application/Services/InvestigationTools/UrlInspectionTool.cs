using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Models;
using System.Net;

namespace Argus.Application.Services.InvestigationTools;

public sealed class UrlInspectionTool : IInvestigationTool
{
    private static readonly HashSet<string> SupportedTaskTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "InspectSuspiciousLinks"
    };

    public string Name => "UrlInspectionTool";

    public bool CanExecute(InvestigationTask task)
    {
        return SupportedTaskTypes.Contains(task.TaskType);
    }

    public Task<ToolResult> ExecuteAsync(InvestigationContext context, InvestigationTask task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var observations = new List<ToolObservation>();
        foreach (var url in context.Findings.Email.Urls)
        {
            if (!Uri.TryCreate(url.Url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var reference = $"url:{url.Url}";
            observations.Add(new ToolObservation("url", url.Url, reference));

            if (IPAddress.TryParse(uri.Host, out _))
            {
                observations.Add(new ToolObservation("ipHost", uri.Host, reference));
            }

            if (uri.Host.Contains("xn--", StringComparison.OrdinalIgnoreCase))
            {
                observations.Add(new ToolObservation("punycodeHost", uri.Host, reference));
            }

            if (!string.IsNullOrWhiteSpace(url.DisplayText)
                && Uri.TryCreate(url.DisplayText, UriKind.Absolute, out var displayUri)
                && !string.Equals(displayUri.Host, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                observations.Add(new ToolObservation("displayMismatch", $"display={displayUri.Host}; destination={uri.Host}", reference));
            }

            var subdomainCount = Math.Max(0, uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries).Length - 2);
            if (subdomainCount > 3)
            {
                observations.Add(new ToolObservation("excessiveSubdomains", subdomainCount.ToString(), reference));
            }
        }

        var indicatorObservations = context.Findings.Indicators
            .Where(indicator => indicator.RuleId.StartsWith("URL-", StringComparison.OrdinalIgnoreCase))
            .Select(indicator => new ToolObservation(
                indicator.RuleId,
                indicator.Evidence ?? indicator.Description,
                $"indicator:{indicator.RuleId}"));

        observations.AddRange(indicatorObservations);

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