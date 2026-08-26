using Argus.Domain.Models;

namespace Argus.Application.Services;

internal static class ResponseEducationPackageValidator
{
    private static readonly HashSet<string> AllowedPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low",
        "Moderate",
        "High",
        "Critical"
    };

    private static readonly HashSet<string> AllowedActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Informational",
        "UserAction",
        "AdministratorAction",
        "ProfessionalEscalation"
    };

    private static readonly HashSet<string> AllowedEscalationLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "None",
        "InternalIT",
        "ManagedServiceProvider",
        "CybersecurityProfessional",
        "LegalOrCompliance",
        "LawEnforcement"
    };

    private static readonly string[] RestrictedActionKeywords =
    [
        "password",
        "credential",
        "session",
        "revoke",
        "disable",
        "mailbox rule",
        "forwarding rule",
        "mfa",
        "multi-factor",
        "account",
        "configuration",
        "firewall",
        "delete",
        "block"
    ];

    private static readonly string[] ProhibitedExecutionClaims =
    [
        "argus reset",
        "argus revoked",
        "argus blocked",
        "argus disabled",
        "argus changed",
        "argus deleted",
        "we reset",
        "we revoked",
        "we blocked",
        "has been reset",
        "have been revoked",
        "was blocked automatically",
        "automatically performed"
    ];

    public static IReadOnlyList<string> Validate(
        ResponseEducationPackage package,
        Guid expectedIncidentId,
        IReadOnlyCollection<string> knownFindingIds)
    {
        var errors = new List<string>();

        if (package.IncidentId != expectedIncidentId)
        {
            errors.Add("Response package IncidentId does not match the requested incident.");
        }

        if (string.IsNullOrWhiteSpace(package.IncidentClassification))
        {
            errors.Add("IncidentClassification is required.");
        }

        if (!AllowedPriority.Contains(package.OverallPriority))
        {
            errors.Add("OverallPriority must be one of Low, Moderate, High, or Critical.");
        }

        if (string.IsNullOrWhiteSpace(package.PlainLanguageSummary))
        {
            errors.Add("PlainLanguageSummary is required.");
        }

        ValidateActions(package.ImmediateActions, "ImmediateActions", knownFindingIds, errors);
        ValidateActions(package.RecoveryActions, "RecoveryActions", knownFindingIds, errors);
        ValidateActions(package.PreventionActions, "PreventionActions", knownFindingIds, errors);

        if (package.EscalationRecommendations is null)
        {
            errors.Add("EscalationRecommendations collection is required.");
        }
        else
        {
            foreach (var escalation in package.EscalationRecommendations)
            {
                if (!AllowedEscalationLevels.Contains(escalation.Level))
                {
                    errors.Add($"Escalation level '{escalation.Level}' is invalid.");
                }

                if (string.IsNullOrWhiteSpace(escalation.Reason))
                {
                    errors.Add($"Escalation recommendation '{escalation.Level}' must include a reason.");
                }

                if (string.IsNullOrWhiteSpace(escalation.RecommendedContact))
                {
                    errors.Add($"Escalation recommendation '{escalation.Level}' must include a recommended contact.");
                }
            }
        }

        if (package.Education is null)
        {
            errors.Add("Education module is required.");
        }
        else
        {
            ValidateEducation(package.Education, knownFindingIds, errors);
        }

        if (package.Assumptions is null)
        {
            errors.Add("Assumptions collection is required.");
        }

        if (package.Limitations is null)
        {
            errors.Add("Limitations collection is required.");
        }

        ValidateProhibitedClaims(package.PlainLanguageSummary, "PlainLanguageSummary", errors);

        return errors;
    }

    private static void ValidateActions(
        IReadOnlyList<ResponseAction>? actions,
        string collectionName,
        IReadOnlyCollection<string> knownFindingIds,
        List<string> errors)
    {
        if (actions is null)
        {
            errors.Add($"{collectionName} collection is required.");
            return;
        }

        var duplicateIds = actions
            .GroupBy(action => action.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            errors.Add($"{collectionName} action IDs must be unique. Duplicate ids: {string.Join(", ", duplicateIds)}.");
        }

        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action.Title))
            {
                errors.Add($"Action {action.Id} in {collectionName} must include a title.");
            }

            if (string.IsNullOrWhiteSpace(action.Description))
            {
                errors.Add($"Action {action.Id} in {collectionName} must include a description.");
            }

            if (string.IsNullOrWhiteSpace(action.Reason))
            {
                errors.Add($"Action {action.Id} in {collectionName} must include a reason.");
            }

            if (action.Priority <= 0)
            {
                errors.Add($"Action {action.Id} in {collectionName} must have a priority greater than zero.");
            }

            if (!AllowedActionTypes.Contains(action.ActionType))
            {
                errors.Add($"Action {action.Id} in {collectionName} has invalid ActionType '{action.ActionType}'.");
            }

            if (action.SupportingFindingIds is null)
            {
                errors.Add($"Action {action.Id} in {collectionName} must include supporting finding ids.");
            }
            else
            {
                foreach (var findingId in action.SupportingFindingIds)
                {
                    if (!knownFindingIds.Contains(findingId))
                    {
                        errors.Add($"Action {action.Id} in {collectionName} references unknown finding '{findingId}'.");
                    }
                }
            }

            if (RequiresHumanApproval(action) && !action.RequiresHumanApproval)
            {
                errors.Add($"Action {action.Id} in {collectionName} must require human approval.");
            }

            ValidateProhibitedClaims(action.Title, $"Action {action.Id} title", errors);
            ValidateProhibitedClaims(action.Description, $"Action {action.Id} description", errors);
            ValidateProhibitedClaims(action.Reason, $"Action {action.Id} reason", errors);
        }
    }

    private static bool RequiresHumanApproval(ResponseAction action)
    {
        var text = $"{action.Title} {action.Description} {action.Reason} {action.ActionType}";
        return RestrictedActionKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateEducation(
        EducationModule education,
        IReadOnlyCollection<string> knownFindingIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(education.Title))
        {
            errors.Add("Education title is required.");
        }

        if (education.EstimatedMinutes <= 0)
        {
            errors.Add("Education estimated minutes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(education.LearningObjective))
        {
            errors.Add("Education learning objective is required.");
        }

        if (string.IsNullOrWhiteSpace(education.Explanation))
        {
            errors.Add("Education explanation is required.");
        }

        if (education.WarningSigns is null || education.WarningSigns.Count == 0)
        {
            errors.Add("Education warning signs are required.");
        }
        else
        {
            foreach (var sign in education.WarningSigns)
            {
                if (string.IsNullOrWhiteSpace(sign.Title))
                {
                    errors.Add("Each warning sign must include a title.");
                }

                if (string.IsNullOrWhiteSpace(sign.Explanation))
                {
                    errors.Add($"Warning sign '{sign.Title}' must include an explanation.");
                }

                if (sign.SupportingFindingIds is null || sign.SupportingFindingIds.Count == 0)
                {
                    errors.Add($"Warning sign '{sign.Title}' must reference at least one supporting finding.");
                }
                else
                {
                    foreach (var findingId in sign.SupportingFindingIds)
                    {
                        if (!knownFindingIds.Contains(findingId))
                        {
                            errors.Add($"Warning sign '{sign.Title}' references unknown finding '{findingId}'.");
                        }
                    }
                }

                ValidateProhibitedClaims(sign.Explanation, $"Warning sign '{sign.Title}'", errors);
            }
        }

        if (education.Questions is null)
        {
            errors.Add("Education questions are required.");
        }
        else
        {
            if (education.Questions.Count < 3 || education.Questions.Count > 5)
            {
                errors.Add("Education must contain between 3 and 5 questions.");
            }

            foreach (var question in education.Questions)
            {
                if (string.IsNullOrWhiteSpace(question.Question))
                {
                    errors.Add($"Education question {question.Id} must include question text.");
                }

                if (question.Options is null || question.Options.Count < 3 || question.Options.Count > 4)
                {
                    errors.Add($"Education question {question.Id} must have 3 to 4 options.");
                }
                else if (question.CorrectOptionIndex < 0 || question.CorrectOptionIndex >= question.Options.Count)
                {
                    errors.Add($"Education question {question.Id} has an invalid correct option index.");
                }

                if (string.IsNullOrWhiteSpace(question.Explanation))
                {
                    errors.Add($"Education question {question.Id} must include an explanation.");
                }

                ValidateProhibitedClaims(question.Explanation, $"Education question {question.Id}", errors);
            }
        }

        if (education.Takeaways is null)
        {
            errors.Add("Education takeaways are required.");
        }
    }

    private static void ValidateProhibitedClaims(string? text, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var phrase in ProhibitedExecutionClaims)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{fieldName} contains a prohibited execution claim.");
                return;
            }
        }
    }
}