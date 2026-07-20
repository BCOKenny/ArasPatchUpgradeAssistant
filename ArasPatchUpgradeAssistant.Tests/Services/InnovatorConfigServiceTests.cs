using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class InnovatorConfigServiceTests
{
    [Fact]
    public void Load_ValidFiles_ReturnsConnectionsAndPrefix()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """
            <Config>
              <DB-Connection id="Main" database="Innovator" server="WIN19SQL2022" />
              <DB-Connection id="Reporting" database="InnovatorReporting" server="SQL02" />
            </Config>
            """);
        temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            """
            <Config>
              <InnovatorServerUrl value="http://localhost/InnovatorServer/Server/InnovatorServer.aspx/" />
            </Config>
            """);

        var result = new InnovatorConfigService().Load(configPath);

        Assert.Equal(
            [
                new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022"),
                new DatabaseConnectionOption("Reporting", "InnovatorReporting", "SQL02")
            ],
            result.Connections);
        Assert.Equal(temp.Path, result.ApServerRoot);
        Assert.Equal($"http://{Environment.MachineName}/InnovatorServer", result.ServerPrefix);
    }

    [Fact]
    public void Load_VaultUrlInNodeText_IsSupported()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """<Config><DB-Connection id="Main" database="Innovator" /></Config>""");
        temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            """
            <Config>
              <InnovatorServerUrl>https://plm.example.com/Server/InnovatorServer.aspx</InnovatorServerUrl>
            </Config>
            """);

        var result = new InnovatorConfigService().Load(configPath);

        Assert.Equal("https://plm.example.com", result.ServerPrefix);
    }

    [Fact]
    public void Load_InvalidConnections_AreExcluded()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """
            <Config>
              <DB-Connection id="" database="EmptyId" />
              <DB-Connection id="MissingDatabase" />
              <DB-Connection id="Valid" database="Innovator" />
            </Config>
            """);
        temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            """<Config><InnovatorServerUrl value="https://plm.example.com/" /></Config>""");

        var result = new InnovatorConfigService().Load(configPath);

        Assert.Equal(new DatabaseConnectionOption("Valid", "Innovator"), result.Connections.Single());
    }

    [Fact]
    public void Load_MissingServerAttribute_ReturnsBlankServerWithoutThrowing()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """<Config><DB-Connection id="Main" database="Innovator" /></Config>""");
        temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            """<Config><InnovatorServerUrl value="https://plm.example.com/" /></Config>""");

        var result = new InnovatorConfigService().Load(configPath);

        Assert.Equal(string.Empty, result.Connections.Single().Server);
    }

    [Fact]
    public void Load_NoValidConnection_ThrowsActionableError()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """<Config><DB-Connection id="MissingDatabase" /></Config>""");
        temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            """<Config><InnovatorServerUrl value="https://plm.example.com/" /></Config>""");

        var exception = Assert.Throws<InvalidDataException>(
            () => new InnovatorConfigService().Load(configPath));

        Assert.Contains("DB-Connection", exception.Message);
    }

    [Fact]
    public void Load_MissingVaultConfig_ThrowsActionableError()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """<Config><DB-Connection id="Main" database="Innovator" /></Config>""");

        var exception = Assert.Throws<FileNotFoundException>(
            () => new InnovatorConfigService().Load(configPath));

        Assert.Contains("vault.config", exception.Message);
    }

    [Fact]
    public void Load_VaultWithoutUrl_ThrowsActionableError()
    {
        using var temp = new TemporaryDirectory();
        var configPath = temp.CreateFile(
            "InnovatorServerConfig.xml",
            """<Config><DB-Connection id="Main" database="Innovator" /></Config>""");
        temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            "<Config />");

        var exception = Assert.Throws<InvalidDataException>(
            () => new InnovatorConfigService().Load(configPath));

        Assert.Contains("InnovatorServerUrl", exception.Message);
    }
}
