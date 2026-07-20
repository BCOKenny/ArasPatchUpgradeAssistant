using ArasPatchUpgradeAssistant.Services;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class LoggingConfiguratorTests
{
    [Fact]
    public void CreateLogger_WritesRollingLogFile()
    {
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "ArasPatchUpgradeAssistant.Tests",
            Guid.NewGuid().ToString("N"),
            "logs");
        var logger = LoggingConfigurator.CreateLogger(logDirectory);

        logger.Information("log smoke test");
        (logger as IDisposable)?.Dispose();

        var logFile = Assert.Single(Directory.EnumerateFiles(logDirectory, "aras-patch-upgrade-assistant-*.log"));
        var content = File.ReadAllText(logFile);
        Assert.Contains("log smoke test", content);
        Assert.Contains("[INF]", content);
    }
}
