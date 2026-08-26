namespace Argus.Domain.Models;

public sealed record EducationModule(
    string Title,
    string AudienceLevel,
    int EstimatedMinutes,
    string LearningObjective,
    string Explanation,
    IReadOnlyList<WarningSign> WarningSigns,
    IReadOnlyList<EducationQuestion> Questions,
    IReadOnlyList<string> Takeaways);