using Argus.Application.Interfaces;
using Argus.Application.Models;
using Argus.Domain.Models;

namespace Argus.Application.Services.InvestigationTools;

public sealed class MitreMappingTool : IInvestigationTool
{
    private static readonly HashSet<string> SupportedTaskTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MapAttackTechniques"
    };

    public string Name => "MitreMappingTool";

    public bool CanExecute(InvestigationTask task)
    {
        return SupportedTaskTypes.Contains(task.TaskType);
    }

    public Task<ToolResult> ExecuteAsync(InvestigationContext context, InvestigationTask task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var observations = context.Findings.MitreTechniques
            .Select(technique => new ToolObservation(
                "mitreTechnique",
                technique,
                $"mitre:{technique}"))
            .ToList();

        var evidenceReferences = observations
            .Select(observation => observation.EvidenceReference)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new ToolResult(
            Name,
            true,
            observations,
            evidenceReferences,
            null));
    }
}