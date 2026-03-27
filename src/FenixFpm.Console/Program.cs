using System.Runtime.InteropServices;
using FenixFpm.ConsoleApp.Configuration;
using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Models;
using FenixFpm.Core.Modules;
using FenixFpm.Core.Services;
using FenixFpm.Infrastructure.Mapping;
using FenixFpm.Infrastructure.SharedMemory;

var options = AppOptions.FromArgs(args);
using var cancellationSource = new CancellationTokenSource();
var renderedEvents = new HashSet<string>(StringComparer.Ordinal);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

IAirbusModule[] modules =
[
    new StabilizedApproachModule(),
    new AutobrakeMonitorModule()
];

var engine = new PerformanceEngine(modules);
await using var reader = new FenixSharedMemoryReader(options.SharedMemoryName);

try
{
    await foreach (var buffer in reader.ReadSnapshotsAsync(options.PollInterval, cancellationSource.Token))
    {
        var snapshot = FenixSnapshotMapper.Map(buffer);
        var result = await engine.EvaluateAsync(snapshot, cancellationSource.Token);
        Render(result, renderedEvents);
    }
}
catch (COMException ex)
{
    Console.WriteLine($"SimConnect error while consuming telemetry: {ex.Message}");
}
catch (OperationCanceledException)
{
}

static void Render(PerformanceEngineResult result, ISet<string> renderedEvents)
{
    foreach (var flightEvent in result.Events)
    {
        var key = $"{flightEvent.Timestamp:O}:{flightEvent.EventType}:{flightEvent.Value:F2}:{flightEvent.Limit:F2}";
        if (!renderedEvents.Add(key))
        {
            continue;
        }

        Console.WriteLine(
            $"{flightEvent.Timestamp:O} [{flightEvent.Severity}] {flightEvent.EventType}: {flightEvent.Description} " +
            $"(value {flightEvent.Value:F2}, limit {flightEvent.Limit:F2})");
    }
}