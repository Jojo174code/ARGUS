using Argus.Domain.Enums;

namespace Argus.Domain.Entities;

public sealed class Incident
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string OrganizationName { get; private set; } = string.Empty;

    public OrganizationType OrganizationType { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string ReportedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public IncidentStatus Status { get; private set; } = IncidentStatus.Submitted;

    public TechnicalSkillLevel TechnicalSkillLevel { get; private set; }

    public ICollection<EvidenceItem> EvidenceItems { get; } = new List<EvidenceItem>();

    public IncidentAnalysis? Analysis { get; private set; }

    private Incident()
    {
    }

    public Incident(
        string organizationName,
        OrganizationType organizationType,
        string description,
        string reportedBy,
        TechnicalSkillLevel technicalSkillLevel)
    {
        OrganizationName = organizationName;
        OrganizationType = organizationType;
        Description = description;
        ReportedBy = reportedBy;
        TechnicalSkillLevel = technicalSkillLevel;
    }

    public void SetStatus(IncidentStatus status)
    {
        Status = status;
    }

    public void AddEvidence(EvidenceItem evidenceItem)
    {
        EvidenceItems.Add(evidenceItem);
    }
}