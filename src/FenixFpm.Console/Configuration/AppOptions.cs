using FenixFpm.Contracts.Interop;

namespace FenixFpm.ConsoleApp.Configuration;

public sealed record AppOptions(string SharedMemoryName, string PerformanceDataPath, TimeSpan PollInterval)
{
    public static AppOptions FromArgs(string[] args)
    {
        var performanceDataPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "data",
                "performance",
                "fenix-a320-landing.json"));

        return new AppOptions(FenixSharedMemoryLayout.MappingName, performanceDataPath, TimeSpan.FromMilliseconds(50));
    }
}