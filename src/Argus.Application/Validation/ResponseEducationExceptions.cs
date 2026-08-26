namespace Argus.Application.Validation;

public sealed class ResponseEducationPrerequisiteException : Exception
{
    public ResponseEducationPrerequisiteException(string message)
        : base(message)
    {
    }
}

public sealed class ResponseEducationValidationException : Exception
{
    public ResponseEducationValidationException(string message, IReadOnlyList<string> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors;
    }

    public IReadOnlyList<string> ValidationErrors { get; }
}

public sealed class ResponseEducationUnavailableException : Exception
{
    public ResponseEducationUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}