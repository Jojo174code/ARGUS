namespace Argus.Application.Validation;

public static class EmailEvidenceValidator
{
    public static IReadOnlyList<string> Validate(string fileName, long fileSize)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            errors.Add("A file name is required.");
        }

        if (!fileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Only .eml files are supported.");
        }

        if (fileSize <= 0)
        {
            errors.Add("Uploaded files must not be empty.");
        }

        return errors;
    }
}