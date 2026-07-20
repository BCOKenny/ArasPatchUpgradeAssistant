using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class VaultConfigServiceTests
{
    [Fact]
    public void GetVaultConfigPath_UsesVaultServerDirectoryUnderInnovatorConfigDirectory()
    {
        var service = new VaultConfigService(Logger.None);

        var result = service.GetVaultConfigPath(
            @"C:\Program Files (x86)\Aras\1209\InnovatorServerConfig.xml");

        Assert.Equal(
            @"C:\Program Files (x86)\Aras\1209\VaultServer\vault.config",
            result);
    }

    [Fact]
    public void ParseInnovatorServerUrl_AppSettingsAddInnovatorServerUrl_ReturnsServerPrefix()
    {
        using var temp = new TemporaryDirectory();
        var vaultPath = temp.CreateFile(
            Path.Combine("VaultServer", "vault.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="InnovatorServerUrl" value="http://localhost/APV8/Server/InnovatorServer.aspx"></add>
                <add key="LocalPath" value="D:\Aras\Vault\"></add>
              </appSettings>
            </configuration>
            """);
        var service = new VaultConfigService(Logger.None);

        var result = service.ParseInnovatorServerUrl(vaultPath);

        Assert.Equal($"http://{Environment.MachineName}/APV8", result);
    }

    [Theory]
    [InlineData("http://localhost:8080/APV8/Server/InnovatorServer.aspx")]
    [InlineData("http://127.0.0.1:8080/APV8/Server/InnovatorServer.aspx")]
    [InlineData("http://[::1]:8080/APV8/Server/InnovatorServer.aspx")]
    public void ParseInnovatorServerUrl_LocalLoopbackHost_IsConvertedToMachineName(string innovatorServerUrl)
    {
        using var temp = new TemporaryDirectory();
        var vaultPath = temp.CreateFile(
            "vault.config",
            $"""
            <configuration>
              <appSettings>
                <add key="InnovatorServerUrl" value="{innovatorServerUrl}" />
              </appSettings>
            </configuration>
            """);
        var service = new VaultConfigService(Logger.None, () => "WIN2019LAB");

        var result = service.ParseInnovatorServerUrl(vaultPath);

        Assert.Equal("http://WIN2019LAB:8080/APV8", result);
    }

    [Fact]
    public void ParseInnovatorServerUrl_RemoteHost_IsPreserved()
    {
        using var temp = new TemporaryDirectory();
        var vaultPath = temp.CreateFile(
            "vault.config",
            """
            <configuration>
              <appSettings>
                <add key="InnovatorServerUrl" value="https://demo2.openplm.com.tw/APV8/Server/InnovatorServer.aspx" />
              </appSettings>
            </configuration>
            """);
        var service = new VaultConfigService(Logger.None, () => "WIN2019LAB");

        var result = service.ParseInnovatorServerUrl(vaultPath);

        Assert.Equal("https://demo2.openplm.com.tw/APV8", result);
    }

    [Fact]
    public void ParseInnovatorServerUrl_UsesLocalNamesAndCaseInsensitiveAttributes()
    {
        using var temp = new TemporaryDirectory();
        var vaultPath = temp.CreateFile(
            "vault.config",
            """
            <x:configuration xmlns:x="urn:test">
              <x:appSettings>
                <x:add KEY="InnovatorServerUrl" VALUE="https://plm.example.com/Server/InnovatorServer.aspx" />
              </x:appSettings>
            </x:configuration>
            """);
        var service = new VaultConfigService(Logger.None);

        var result = service.ParseInnovatorServerUrl(vaultPath);

        Assert.Equal("https://plm.example.com", result);
    }
}
