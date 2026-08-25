namespace Argus.Application.Validation;

public sealed class CoordinatorPrerequisiteException : Exception
{
    public CoordinatorPrerequisiteException(string message)
        : base(message)
    {
    }
}

public sealed class CoordinatorPlanValidationException : Exception
{
    public CoordinatorPlanValidationException(string message, IReadOnlyList<string> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors;
    }

    public IReadOnlyList<string> ValidationErrors { get; }
}

public sealed class CoordinatorUnavailableException : Exception
{
    public CoordinatorUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}