using System.IO;
using Serilog;
using Serilog.Events;

namespace ArasPatchUpgradeAssistant.Services;

public static class LoggingConfigurator
{
    public const string LogFileNamePattern = "aras-patch-upgrade-assistant-.log";

    public static ILogger CreateLogger(string? logDirectory = null)
    {
        var directory = logDirectory ?? GetDefaultLogDirectory();
        Directory.CreateDirectory(directory);

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug(
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(directory, LogFileNamePattern),
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static string GetDefaultLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(
            localAppData,
            "ArasPatchUpgradeAssistant",
            "logs");
    }
}
