namespace ArasPatchUpgradeAssistant.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ApplicationAssembly_CanBeLoaded()
    {
        Assert.NotNull(typeof(App).Assembly);
    }
}
