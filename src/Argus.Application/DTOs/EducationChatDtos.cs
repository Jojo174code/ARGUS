namespace Argus.Application.DTOs;

public sealed record EducationChatMessageDto(
    string Role,
    string Content);

public sealed record EducationChatRequestDto(
    string Question,
    IReadOnlyList<EducationChatMessageDto>? History);

public sealed record EducationChatResponseDto(
    string Answer,
    string? SuggestedNextStep,
    string Model,
    DateTimeOffset GeneratedAt);
