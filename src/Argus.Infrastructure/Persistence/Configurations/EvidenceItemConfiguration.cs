using Argus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Argus.Infrastructure.Persistence.Configurations;

public sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence_items");

        builder.HasKey(evidence => evidence.Id);

        builder.Property(evidence => evidence.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(evidence => evidence.ContentType)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(evidence => evidence.EvidenceType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(evidence => evidence.StoredPath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(evidence => evidence.Sha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(evidence => evidence.FileSize)
            .IsRequired();

        builder.Property(evidence => evidence.UploadedAt)
            .IsRequired();
    }
}