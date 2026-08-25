using Argus.Application.Models;
using Argus.Domain.Models;

namespace Argus.Application.Interfaces;

public interface IInvestigationTool
{
    string Name { get; }

    bool CanExecute(InvestigationTask task);

    Task<ToolResult> ExecuteAsync(
        InvestigationContext context,
        InvestigationTask task,
        CancellationToken cancellationToken);
}