using Argus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Infrastructure.Persistence;

public sealed class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options)
        : base(options)
    {
    }

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();

    public DbSet<IncidentAnalysis> IncidentAnalyses => Set<IncidentAnalysis>();

    public DbSet<CoordinatorRun> CoordinatorRuns => Set<CoordinatorRun>();

    public DbSet<InvestigatorRun> InvestigatorRuns => Set<InvestigatorRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArgusDbContext).Assembly);
    }
}