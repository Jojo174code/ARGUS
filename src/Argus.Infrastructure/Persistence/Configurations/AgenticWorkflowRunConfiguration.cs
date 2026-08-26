using Argus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Infrastructure.Persistence.Configurations;

public sealed class AgenticWorkflowRunConfiguration : IEntityTypeConfiguration<AgenticWorkflowRun>
{
    public void Configure(EntityTypeBuilder<AgenticWorkflowRun> builder)
    {
        builder.ToTable("agentic_workflow_runs");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(run => run.CurrentStage)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(run => run.StageResultsJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(run => run.BlockingMissingInformationJson)
            .HasColumnType("text");

        builder.Property(run => run.SummaryMetricsJson)
            .HasColumnType("text");

        builder.Property(run => run.FailureStage)
            .HasMaxLength(100);

        builder.Property(run => run.FailureMessage)
            .HasMaxLength(4000);

        builder.HasIndex(run => new { run.IncidentId, run.StartedAt });

        builder.HasOne(run => run.Incident)
            .WithMany()
            .HasForeignKey(run => run.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}