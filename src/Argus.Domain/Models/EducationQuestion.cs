namespace Argus.Domain.Models;

public sealed record EducationQuestion(
    string Id,
    string Question,
    IReadOnlyList<string> Options,
    int CorrectOptionIndex,
    string Explanation);