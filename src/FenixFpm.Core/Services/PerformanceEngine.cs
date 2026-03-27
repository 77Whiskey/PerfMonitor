using FenixFpm.Core.Abstractions;
using FenixFpm.Core.Modules;
using FenixFpm.Core.Models;

namespace FenixFpm.Core.Services;

public sealed class PerformanceEngine
{
    private readonly IReadOnlyList<IAirbusModule> _modules;

    public PerformanceEngine(IEnumerable<IAirbusModule> modules)
    {
        _modules = modules.ToArray();
    }

    public async ValueTask<PerformanceEngineResult> EvaluateAsync(
        FlightSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var events = new List<FlightEvent>();
        foreach (var module in _modules)
        {
            events.AddRange(await module.EvaluateAsync(snapshot, cancellationToken));
        }

        return new PerformanceEngineResult(snapshot.TimestampUtc, events);
    }
}

public static class PerformanceEngineExtensions
{
    public static PerformanceEngine CreateWithSopConfiguration(
        this IEnumerable<IAirbusModule> modules,
        StabilizedApproachCriteria criteria)
    {
        var moduleList = modules.ToList();
        
        var existingStabilizedModule = moduleList.OfType<StabilizedApproachModule>().FirstOrDefault();
        if (existingStabilizedModule != null)
        {
            moduleList.Remove(existingStabilizedModule);
        }
        
        moduleList.Add(new StabilizedApproachModule(criteria));
        
        return new PerformanceEngine(moduleList);
    }
}
