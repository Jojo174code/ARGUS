namespace Argus.Application.Validation;

public sealed class InvestigatorPrerequisiteException : Exception
{
    public InvestigatorPrerequisiteException(string message)
        : base(message)
    {
    }
}

public sealed class InvestigatorValidationException : Exception
{
    public InvestigatorValidationException(string message, IReadOnlyList<string> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors;
    }

    public IReadOnlyList<string> ValidationErrors { get; }
}

public sealed class InvestigatorUnavailableException : Exception
{
    public InvestigatorUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}