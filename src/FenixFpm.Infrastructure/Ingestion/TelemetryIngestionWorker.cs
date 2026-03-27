using System.Threading.Channels;
using FenixFpm.Contracts.Interop;
using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;
using FenixFpm.Core.Services;
using FenixFpm.Infrastructure.Mapping;
using FenixFpm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace FenixFpm.Infrastructure.Ingestion;

public sealed class TelemetryIngestionWorker : BackgroundService
{
    private const int MaxBatchSize = 60;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SnapshotThrottleInterval = TimeSpan.FromMilliseconds(100);

    private readonly Channel<FenixFpmSharedBuffer> _telemetryChannel;
    private readonly IDbContextFactory<FenixTelemetryDbContext> _dbContextFactory;
    private readonly PerformanceEngine _engine;
    private readonly FlightPhaseDetector _phaseDetector;
    private readonly IPerformanceDataService _performanceDataService;

    private FlightPhase _currentPhase = FlightPhase.Unknown;
    private DateTimeOffset _lastSnapshotSavedTime = DateTimeOffset.MinValue;

    public event EventHandler<FenixFpmSharedBuffer>? TelemetryReceived;
    public event EventHandler<FlightSnapshot>? SnapshotReceived;
    public event EventHandler<FlightEvent>? EventDetected;
    public event EventHandler<FlightSession>? SessionStarted;
    public event EventHandler<FlightSession>? SessionCompleted;
    public event EventHandler<FlightPhase>? PhaseChanged;

    public TelemetryIngestionWorker(
        Channel<FenixFpmSharedBuffer> telemetryChannel,
        IDbContextFactory<FenixTelemetryDbContext> dbContextFactory,
        PerformanceEngine engine,
        IPerformanceDataService performanceDataService)
    {
        _telemetryChannel = telemetryChannel;
        _dbContextFactory = dbContextFactory;
        _engine = engine;
        _performanceDataService = performanceDataService;
        _phaseDetector = new FlightPhaseDetector();
    }

    public FlightPhase CurrentPhase => _currentPhase;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
        await db.Database.EnsureCreatedAsync(stoppingToken);

        Guid? sessionId = null;
        DateTimeOffset? startedAtUtc = null;
        DateTimeOffset? endedAtUtc = null;
        var snapshotCount = 0;
        var batch = new List<TelemetrySnapshotEntity>(MaxBatchSize);
        var pendingEvents = new List<FlightEventEntity>(MaxBatchSize);
        using var flushTimer = new PeriodicTimer(FlushInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_telemetryChannel.Reader.TryRead(out var buffer))
                {
                    TelemetryReceived?.Invoke(this, buffer);
                    var snapshot = FenixSnapshotMapper.Map(buffer);

                    var newPhase = _phaseDetector.DetectPhase(snapshot, _currentPhase);
                    if (newPhase != _currentPhase)
                    {
                        await HandlePhaseTransitionAsync(sessionId, newPhase, stoppingToken);
                        _currentPhase = newPhase;
                        PhaseChanged?.Invoke(this, _currentPhase);
                    }

                    SnapshotReceived?.Invoke(this, snapshot);

                    if (sessionId is null && newPhase != FlightPhase.Ground)
                    {
                        sessionId = Guid.NewGuid();
                        startedAtUtc = snapshot.TimestampUtc;
                        sessionId = await CreateFlightSessionAsync(sessionId.Value, startedAtUtc.Value, stoppingToken);
                        var session = new FlightSession(sessionId.Value, startedAtUtc.Value, null, "Fenix A320", 0);
                        SessionStarted?.Invoke(this, session);
                    }

                    if (sessionId.HasValue)
                    {
                        endedAtUtc = snapshot.TimestampUtc;
                        snapshotCount++;

                        if (DateTimeOffset.UtcNow - _lastSnapshotSavedTime >= SnapshotThrottleInterval)
                        {
                            batch.Add(TelemetrySnapshotEntity.FromDomain(sessionId.Value, snapshot));
                            _lastSnapshotSavedTime = DateTimeOffset.UtcNow;
                        }

                        var result = await _engine.EvaluateAsync(snapshot, stoppingToken);
                        foreach (var evt in result.Events)
                        {
                            var enhancedEvent = evt with { FlightPhase = _currentPhase.ToString() };
                            pendingEvents.Add(FlightEventEntity.FromModel(sessionId.Value, enhancedEvent));
                            EventDetected?.Invoke(this, enhancedEvent);
                        }
                    }

                    if (batch.Count >= MaxBatchSize)
                    {
                        await FlushBatchAsync(sessionId!.Value, batch, pendingEvents, startedAtUtc!.Value, endedAtUtc!.Value, snapshotCount, stoppingToken);
                    }
                }

                var waitToReadTask = _telemetryChannel.Reader.WaitToReadAsync(stoppingToken).AsTask();
                var tickTask = flushTimer.WaitForNextTickAsync(stoppingToken).AsTask();
                var completedTask = await Task.WhenAny(waitToReadTask, tickTask);

                if (completedTask == tickTask)
                {
                    if (sessionId.HasValue && batch.Count > 0 && startedAtUtc.HasValue && endedAtUtc.HasValue)
                    {
                        await FlushBatchAsync(sessionId.Value, batch, pendingEvents, startedAtUtc.Value, endedAtUtc.Value, snapshotCount, stoppingToken);
                    }

                    if (!await tickTask)
                    {
                        break;
                    }

                    continue;
                }

                if (!await waitToReadTask)
                {
                    break;
                }
            }
        }
        finally
        {
            if (sessionId.HasValue && batch.Count > 0 && startedAtUtc.HasValue && endedAtUtc.HasValue)
            {
                await FlushBatchAsync(sessionId.Value, batch, pendingEvents, startedAtUtc.Value, endedAtUtc.Value, snapshotCount, stoppingToken);
            }

            if (sessionId.HasValue)
            {
                await CompleteFlightSessionAsync(sessionId.Value, snapshotCount, stoppingToken);
            }
        }
    }

    private async Task<Guid> CreateFlightSessionAsync(Guid sessionId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.FlightSessions.Add(new FlightSessionEntity
        {
            Id = sessionId,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc,
            SnapshotCount = 0,
            AircraftType = "Fenix A320"
        });

        await db.SaveChangesAsync(cancellationToken);
        return sessionId;
    }

    private async Task HandlePhaseTransitionAsync(Guid? sessionId, FlightPhase newPhase, CancellationToken cancellationToken)
    {
        if (newPhase == FlightPhase.Ground && _currentPhase is FlightPhase.Rollout or FlightPhase.Landing && sessionId.HasValue)
        {
            await CompleteFlightSessionAsync(sessionId.Value, 0, cancellationToken);
        }
    }

    private async Task CompleteFlightSessionAsync(Guid sessionId, int snapshotCount, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.FlightSessions
            .Include(s => s.Snapshots)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return;
        }

        session.EndedAtUtc = DateTimeOffset.UtcNow;
        session.SnapshotCount = snapshotCount > 0 ? session.Snapshots.Count : 0;

        var warningCount = await db.FlightEvents
            .CountAsync(x => x.FlightSessionId == sessionId && x.Severity == (int)EventSeverity.Warning, cancellationToken);

        var advisoryCount = await db.FlightEvents
            .CountAsync(x => x.FlightSessionId == sessionId && x.Severity == (int)EventSeverity.Advisory, cancellationToken);

        var completedSession = new FlightSession(
            session.Id,
            session.StartedAtUtc,
            session.EndedAtUtc,
            session.AircraftType,
            session.SnapshotCount)
        {
            CurrentPhase = _currentPhase,
            WarningCount = warningCount,
            AdvisoryCount = advisoryCount
        };

        await db.SaveChangesAsync(cancellationToken);
        SessionCompleted?.Invoke(this, completedSession);
    }

    private async Task FlushBatchAsync(
        Guid sessionId,
        List<TelemetrySnapshotEntity> batch,
        List<FlightEventEntity> pendingEvents,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        int snapshotCount,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0 && pendingEvents.Count == 0)
        {
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (batch.Count > 0)
        {
            db.TelemetrySnapshots.AddRange(batch);
        }

        if (pendingEvents.Count > 0)
        {
            db.FlightEvents.AddRange(pendingEvents);
        }

        var session = await db.FlightSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        session.StartedAtUtc = startedAtUtc;
        session.EndedAtUtc = endedAtUtc;
        session.SnapshotCount = snapshotCount;

        await db.SaveChangesAsync(cancellationToken);
        batch.Clear();
        pendingEvents.Clear();
    }
}
