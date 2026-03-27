using Microsoft.EntityFrameworkCore;

namespace FenixFpm.Infrastructure.Persistence;

public sealed class FenixTelemetryDbContext : DbContext
{
    public FenixTelemetryDbContext(DbContextOptions<FenixTelemetryDbContext> options)
        : base(options)
    {
    }

    public DbSet<FlightSessionEntity> FlightSessions => Set<FlightSessionEntity>();
    public DbSet<TelemetrySnapshotEntity> TelemetrySnapshots => Set<TelemetrySnapshotEntity>();
    public DbSet<FlightEventEntity> FlightEvents => Set<FlightEventEntity>();
    public DbSet<PerformanceMetricsEntity> PerformanceMetrics => Set<PerformanceMetricsEntity>();
    public DbSet<FlightPhaseEntity> FlightPhases => Set<FlightPhaseEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FlightSessionEntity>(entity =>
        {
            entity.ToTable("FlightSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AircraftType).HasMaxLength(128);
            entity.Property(x => x.TailNumber).HasMaxLength(32);
            entity.Property(x => x.FlightNumber).HasMaxLength(16);
            entity.Property(x => x.PilotId).HasMaxLength(64);
            entity.Property(x => x.TotalFlightTime).HasConversion<long>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasMany(x => x.Snapshots)
                .WithOne("FlightSession")
                .HasForeignKey(x => x.FlightSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Events)
                .WithOne("FlightSession")
                .HasForeignKey(x => x.FlightSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Phases)
                .WithOne("FlightSession")
                .HasForeignKey(x => x.FlightSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TelemetrySnapshotEntity>(entity =>
        {
            entity.ToTable("TelemetrySnapshots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FlightSessionId, x.TimestampUtc });
        });

        modelBuilder.Entity<FlightEventEntity>(entity =>
        {
            entity.ToTable("FlightEvents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(1024);
            entity.Property(x => x.FlightPhase).HasMaxLength(64);
            entity.HasIndex(x => new { x.FlightSessionId, x.TimestampUtc });
        });

        modelBuilder.Entity<PerformanceMetricsEntity>(entity =>
        {
            entity.ToTable("PerformanceMetrics");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.FlightSessionId);
        });

        modelBuilder.Entity<FlightPhaseEntity>(entity =>
        {
            entity.ToTable("FlightPhases");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FlightSessionId, x.StartedAtUtc });
        });
    }
}