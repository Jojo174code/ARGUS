using Argus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Infrastructure.Persistence.Configurations;

public sealed class IncidentAnalysisConfiguration : IEntityTypeConfiguration<IncidentAnalysis>
{
    public void Configure(EntityTypeBuilder<IncidentAnalysis> builder)
    {
        builder.ToTable("incident_analyses");

        builder.HasKey(analysis => analysis.Id);

        builder.HasIndex(analysis => analysis.IncidentId)
            .IsUnique();

        builder.Property(analysis => analysis.RiskLevel)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(analysis => analysis.Summary)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(analysis => analysis.ParsedEmailJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(analysis => analysis.RuleResultsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(analysis => analysis.MitreMappingsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.HasOne(analysis => analysis.Incident)
            .WithOne(incident => incident.Analysis)
            .HasForeignKey<IncidentAnalysis>(analysis => analysis.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}