using Argus.Domain.Models;

namespace Argus.Application.Services;

internal static class InvestigationReportValidator
{
    private static readonly HashSet<string> AllowedSeverity = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low",
        "Moderate",
        "High",
        "Critical"
    };

    public static IReadOnlyList<string> Validate(
        InvestigationReport report,
        Guid expectedIncidentId,
        InvestigationPlan coordinatorPlan,
        IReadOnlyCollection<string> knownEvidenceReferences)
    {
        var errors = new List<string>();

        if (report.IncidentId != expectedIncidentId)
        {
            errors.Add("Investigation report IncidentId does not match the requested incident.");
        }

        if (string.IsNullOrWhiteSpace(report.Classification))
        {
            errors.Add("Classification is required.");
        }

        if (!AllowedSeverity.Contains(report.Severity))
        {
            errors.Add("Severity must be one of Low, Moderate, High, or Critical.");
        }

        if (report.Confidence is < 0 or > 1)
        {
            errors.Add("Confidence must be between 0 and 1.");
        }

        if (report.Findings is null)
        {
            errors.Add("Findings collection is required.");
        }
        else
        {
            var duplicateFindingIds = report.Findings
                .GroupBy(finding => finding.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateFindingIds.Count > 0)
            {
                errors.Add($"Finding IDs must be unique. Duplicate ids: {string.Join(", ", duplicateFindingIds)}.");
            }

            var knownTaskIds = coordinatorPlan.Tasks.Select(task => task.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var finding in report.Findings)
            {
                if (string.IsNullOrWhiteSpace(finding.Title))
                {
                    errors.Add($"Finding {finding.Id} must include a title.");
                }

                if (!AllowedSeverity.Contains(finding.Severity))
                {
                    errors.Add($"Finding {finding.Id} has an invalid severity.");
                }

                if (finding.Confidence is < 0 or > 1)
                {
                    errors.Add($"Finding {finding.Id} confidence must be between 0 and 1.");
                }

                if (string.IsNullOrWhiteSpace(finding.EvidenceSource) || string.IsNullOrWhiteSpace(finding.EvidenceReference))
                {
                    errors.Add($"Finding {finding.Id} must include evidence source and evidence reference.");
                }

                if (!string.IsNullOrWhiteSpace(finding.EvidenceReference) && !knownEvidenceReferences.Contains(finding.EvidenceReference))
                {
                    errors.Add($"Finding {finding.Id} references unknown evidence '{finding.EvidenceReference}'.");
                }

                if (!string.IsNullOrWhiteSpace(finding.TaskId) && !knownTaskIds.Contains(finding.TaskId))
                {
                    errors.Add($"Finding {finding.Id} references unknown task '{finding.TaskId}'.");
                }
            }
        }

        if (report.AttackTechniques is null)
        {
            errors.Add("AttackTechniques collection is required.");
        }
        else
        {
            foreach (var technique in report.AttackTechniques)
            {
                if (string.IsNullOrWhiteSpace(technique.TechniqueId)
                    || !System.Text.RegularExpressions.Regex.IsMatch(technique.TechniqueId, "^T\\d{4}(\\.\\d{3})?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                {
                    errors.Add($"Attack technique id '{technique.TechniqueId}' is not in a supported format.");
                }
            }
        }

        if (report.PossibleImpact is null)
        {
            errors.Add("PossibleImpact collection is required.");
        }

        if (report.Uncertainties is null)
        {
            errors.Add("Uncertainties collection is required.");
        }

        if (report.TaskResults is null)
        {
            errors.Add("TaskResults collection is required.");
        }
        else
        {
            var duplicateTaskResults = report.TaskResults
                .GroupBy(result => result.TaskId, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateTaskResults.Count > 0)
            {
                errors.Add($"Task result IDs must be unique. Duplicate ids: {string.Join(", ", duplicateTaskResults)}.");
            }

            foreach (var taskResult in report.TaskResults)
            {
                if (string.Equals(taskResult.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                    && taskResult.ToolsUsed.Count == 0)
                {
                    errors.Add($"Task result '{taskResult.TaskId}' is Completed but has no tools used.");
                }

                if (string.Equals(taskResult.Status, "Unsupported", StringComparison.OrdinalIgnoreCase)
                    && taskResult.ToolsUsed.Count > 0)
                {
                    errors.Add($"Task result '{taskResult.TaskId}' is Unsupported but includes executed tools.");
                }
            }
        }

        return errors;
    }
}