using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;
using FenixFpm.Core.Modules;
using FenixFpm.Core.Services;
using FenixFpm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FenixFpm.Infrastructure.Debrief;

public sealed class DebriefService : IDebriefService
{
    private readonly IDbContextFactory<FenixTelemetryDbContext> _dbContextFactory;
    private readonly IAnalyticsService _analyticsService;

    public DebriefService(IDbContextFactory<FenixTelemetryDbContext> dbContextFactory, IAnalyticsService analyticsService)
    {
        _dbContextFactory = dbContextFactory;
        _analyticsService = analyticsService;
    }

    public async Task<FlightSession?> GetLatestFlightAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.FlightSessions
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return session?.ToModel();
    }

    public async Task<IEnumerable<FlightSession>> GetRecentFlightsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sessions = await db.FlightSessions
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

        return sessions.Select(e => e.ToModel()).ToList();
    }

    public async Task<IEnumerable<FlightEvent>> GetFlightEventsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var eventEntities = await db.FlightEvents
            .AsNoTracking()
            .Where(x => x.FlightSessionId == sessionId)
            .OrderBy(x => x.TimestampUtc)
            .ToListAsync(cancellationToken);

        return eventEntities.Select(e => e.ToModel()).ToList();
    }

    public async Task<PerformanceMetrics?> GetFlightMetricsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        var snapshots = await db.TelemetrySnapshots
            .AsNoTracking()
            .Where(x => x.FlightSessionId == sessionId)
            .OrderBy(x => x.TimestampUtc)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0) return null;

        var events = await db.FlightEvents
            .AsNoTracking()
            .Where(x => x.FlightSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var flightSnapshots = snapshots.Select(TelemetrySnapshotEntity.ToDomain).ToList();
        var flightEvents = events.Select(e => e.ToModel()).ToList();

        return await _analyticsService.CalculateMetricsAsync(flightSnapshots, flightEvents, cancellationToken);
    }

    public async Task<Scorecard?> GetFlightScorecardAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var snapshots = await db.TelemetrySnapshots
            .AsNoTracking()
            .Where(x => x.FlightSessionId == sessionId)
            .OrderBy(x => x.TimestampUtc)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0) return null;

        var events = await db.FlightEvents
            .AsNoTracking()
            .Where(x => x.FlightSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var flightSnapshots = snapshots.Select(TelemetrySnapshotEntity.ToDomain).ToList();
        var flightEvents = events.Select(e => e.ToModel()).ToList();

        return await _analyticsService.GenerateScorecardAsync(sessionId, flightSnapshots, flightEvents, cancellationToken);
    }
}