using Argus.Application.Interfaces;
using Argus.Domain.Models;

namespace Argus.Infrastructure.Email;

public sealed class StaticMitreAttackMapper : IMitreAttackMapper
{
    private static readonly MitreAttackTechnique Phishing = new(
        "T1566",
        "Phishing",
        "Adversaries send fraudulent messages to entice interaction or credential disclosure.");

    private static readonly MitreAttackTechnique SpearphishingAttachment = new(
        "T1566.001",
        "Spearphishing Attachment",
        "Adversaries send a malicious or suspicious attachment to a targeted recipient.");

    private static readonly MitreAttackTechnique SpearphishingLink = new(
        "T1566.002",
        "Spearphishing Link",
        "Adversaries send a message containing a link to attacker-controlled content.");

    private static readonly MitreAttackTechnique WebPortalCapture = new(
        "T1056.003",
        "Web Portal Capture",
        "Adversaries attempt credential collection through deceptive or lookalike login pages.");

    public IReadOnlyList<MitreAttackTechnique> Map(ParsedEmail email, IReadOnlyList<PhishingRuleResult> ruleResults)
    {
        var triggeredRules = ruleResults.Where(rule => rule.Triggered).ToList();
        if (triggeredRules.Count == 0)
        {
            return Array.Empty<MitreAttackTechnique>();
        }

        var techniques = new List<MitreAttackTechnique> { Phishing };

        if (email.Attachments.Count > 0)
        {
            techniques.Add(SpearphishingAttachment);
        }

        if (email.Urls.Count > 0)
        {
            techniques.Add(SpearphishingLink);
        }

        if (triggeredRules.Any(rule => rule.RuleId is "URL-DISPLAY-001" or "URL-BRAND-001" or "SOCIAL-LOGIN-001"))
        {
            techniques.Add(WebPortalCapture);
        }

        return techniques.DistinctBy(technique => technique.TechniqueId).ToList();
    }
}