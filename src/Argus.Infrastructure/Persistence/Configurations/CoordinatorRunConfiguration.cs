using Argus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Infrastructure.Persistence.Configurations;

public sealed class CoordinatorRunConfiguration : IEntityTypeConfiguration<CoordinatorRun>
{
    public void Configure(EntityTypeBuilder<CoordinatorRun> builder)
    {
        builder.ToTable("coordinator_runs");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.Model)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(run => run.PromptVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(run => run.InputJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(run => run.OutputJson)
            .HasColumnType("text");

        builder.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(run => run.ErrorMessage)
            .HasMaxLength(4000);

        builder.HasIndex(run => new { run.IncidentId, run.StartedAt });

        builder.HasOne(run => run.Incident)
            .WithMany()
            .HasForeignKey(run => run.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}