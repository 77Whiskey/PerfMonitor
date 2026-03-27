using FenixFpm.Core.Models;
using FenixFpm.Core.Services;
using FenixFpm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FenixFpm.Infrastructure.Persistence;

public interface IFlightSessionRepository
{
    Task<FlightSessionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FlightSessionEntity?> GetActiveSessionAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<FlightSessionEntity>> GetRecentSessionsAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<FlightSessionEntity>> GetCompletedSessionsAsync(int count = 50, CancellationToken cancellationToken = default);
    Task<FlightSessionEntity> CreateAsync(FlightSessionEntity session, CancellationToken cancellationToken = default);
    Task UpdateAsync(FlightSessionEntity session, CancellationToken cancellationToken = default);
    Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default);
}

public sealed class FlightSessionRepository : IFlightSessionRepository
{
    private readonly IDbContextFactory<FenixTelemetryDbContext> _contextFactory;

    public FlightSessionRepository(IDbContextFactory<FenixTelemetryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<FlightSessionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FlightSessions
            .Include(s => s.Events)
            .Include(s => s.Phases)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<FlightSessionEntity?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FlightSessions
            .Include(s => s.Events)
            .FirstOrDefaultAsync(x => x.Status == FlightSessionStatus.Active, cancellationToken);
    }

    public async Task<IEnumerable<FlightSessionEntity>> GetRecentSessionsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FlightSessions
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FlightSessionEntity>> GetCompletedSessionsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FlightSessions
            .Where(x => x.Status == FlightSessionStatus.Completed)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<FlightSessionEntity> CreateAsync(FlightSessionEntity session, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.FlightSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task UpdateAsync(FlightSessionEntity session, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.FlightSessions.Update(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FlightSessions.CountAsync(cancellationToken);
    }
}