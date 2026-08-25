using Argus.Domain.Models;

namespace Argus.Application.Interfaces;

public interface IMitreAttackMapper
{
    IReadOnlyList<MitreAttackTechnique> Map(ParsedEmail email, IReadOnlyList<PhishingRuleResult> ruleResults);
}