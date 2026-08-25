using Argus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(incident => incident.Id);

        builder.Property(incident => incident.OrganizationName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(incident => incident.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(incident => incident.ReportedBy)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(incident => incident.OrganizationType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(incident => incident.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(incident => incident.TechnicalSkillLevel)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(incident => incident.CreatedAt)
            .IsRequired();

        builder.HasMany(incident => incident.EvidenceItems)
            .WithOne(evidence => evidence.Incident)
            .HasForeignKey(evidence => evidence.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(incident => incident.Analysis)
            .WithOne(analysis => analysis.Incident)
            .HasForeignKey<IncidentAnalysis>(analysis => analysis.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}