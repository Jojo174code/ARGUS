using Argus.Domain.Models;

namespace Argus.Application.Services;

internal static class CoordinatorPlanValidator
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low",
        "Moderate",
        "Medium",
        "High",
        "Critical"
    };

    public static IReadOnlyList<string> Validate(InvestigationPlan plan, Guid expectedIncidentId)
    {
        var validationErrors = new List<string>();

        if (plan.IncidentId != expectedIncidentId)
        {
            validationErrors.Add("Incident identifier in the plan does not match the requested incident.");
        }

        if (string.IsNullOrWhiteSpace(plan.IncidentType))
        {
            validationErrors.Add("IncidentType must be provided.");
        }

        if (string.IsNullOrWhiteSpace(plan.Priority) || !AllowedPriorities.Contains(plan.Priority))
        {
            validationErrors.Add("Priority must be one of Low, Moderate, Medium, High, or Critical.");
        }

        if (plan.Tasks is null)
        {
            validationErrors.Add("Tasks must be provided.");
        }
        else
        {
            var duplicateTaskIds = plan.Tasks
                .GroupBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateTaskIds.Count > 0)
            {
                validationErrors.Add($"Task identifiers must be unique. Duplicate ids: {string.Join(", ", duplicateTaskIds)}.");
            }

            foreach (var task in plan.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Id))
                {
                    validationErrors.Add("Each task requires a non-empty Id.");
                }

                if (string.IsNullOrWhiteSpace(task.TaskType))
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires a non-empty TaskType.");
                }

                if (string.IsNullOrWhiteSpace(task.Title))
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires a non-empty Title.");
                }

                if (string.IsNullOrWhiteSpace(task.Description))
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires a non-empty Description.");
                }

                if (task.Priority <= 0)
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires a Priority greater than zero.");
                }

                if (task.RequiredEvidence is null)
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires a RequiredEvidence collection.");
                }

                if (string.IsNullOrWhiteSpace(task.AssignedCapability))
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires an AssignedCapability.");
                }

                if (string.IsNullOrWhiteSpace(task.CompletionCondition))
                {
                    validationErrors.Add($"Task {task.Id ?? "<unknown>"} requires a CompletionCondition.");
                }
            }
        }

        if (plan.MissingInformation is null)
        {
            validationErrors.Add("MissingInformation must be provided.");
        }

        if (plan.Assumptions is null)
        {
            validationErrors.Add("Assumptions must be provided.");
        }

        if (plan.SafetyNotes is null)
        {
            validationErrors.Add("SafetyNotes must be provided.");
        }

        if (plan.MissingInformation is not null && plan.MissingInformation.Any(item => item.Required) && plan.ReadyForInvestigation)
        {
            validationErrors.Add("ReadyForInvestigation cannot be true when required missing information remains.");
        }

        return validationErrors;
    }
}