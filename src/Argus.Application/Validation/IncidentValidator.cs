using Argus.Application.DTOs;

namespace Argus.Application.Validation;

public static class IncidentValidator
{
    public static IReadOnlyList<string> Validate(CreateIncidentRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            errors.Add("Organization name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("Description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ReportedBy))
        {
            errors.Add("Reporter name is required.");
        }

        return errors;
    }
}