using System.IO;
using System.Net;
using System.Xml.Linq;
using Serilog;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class VaultConfigService : IVaultConfigService
{
    private const string InnovatorSuffix = "/Server/InnovatorServer.aspx";
    private readonly ILogger _logger;
    private readonly Func<string> _machineNameProvider;

    public VaultConfigService(
        ILogger? logger = null,
        Func<string>? machineNameProvider = null)
    {
        _logger = logger ?? Log.ForContext<VaultConfigService>();
        _machineNameProvider = machineNameProvider ?? (() => Environment.MachineName);
    }

    public string GetVaultConfigPath(string innovatorServerConfigPath)
    {
        if (string.IsNullOrWhiteSpace(innovatorServerConfigPath))
        {
            throw new ArgumentException(
                "請先選擇 InnovatorServerConfig.xml。",
                nameof(innovatorServerConfigPath));
        }

        var apRoot = Path.GetDirectoryName(Path.GetFullPath(innovatorServerConfigPath));
        if (string.IsNullOrWhiteSpace(apRoot))
        {
            throw new InvalidDataException(
                $"無法從 InnovatorServerConfig.xml 路徑推導 AP Server Root：{innovatorServerConfigPath}");
        }

        var vaultConfigPath = Path.Combine(apRoot, "VaultServer", "vault.config");
        _logger.Information(
            "Derived vault.config path {VaultConfigPath}",
            vaultConfigPath);
        return vaultConfigPath;
    }

    public string ParseInnovatorServerUrl(string vaultConfigPath)
    {
        _logger.Information("Parse vault.config started {VaultConfigPath}", vaultConfigPath);

        try
        {
            var fullPath = Path.GetFullPath(vaultConfigPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"vault.config 不存在：{fullPath}",
                    fullPath);
            }

            var vaultUrl = ReadVaultUrl(XDocument.Load(fullPath));
            if (string.IsNullOrWhiteSpace(vaultUrl))
            {
                throw new InvalidDataException(
                    $"找不到 InnovatorServerUrl，請確認 vault.config：{fullPath}");
            }

            var serverPrefix = NormalizeServerPrefix(vaultUrl);
            _logger.Information(
                "Parse vault.config completed {VaultConfigPath}",
                fullPath);
            return serverPrefix;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Parse vault.config failed {VaultConfigPath}",
                vaultConfigPath);
            throw;
        }
    }

    private static string? ReadVaultUrl(XDocument document)
    {
        if (document.Root is null)
        {
            return null;
        }

        var appSettings = document.Root
            .Descendants()
            .FirstOrDefault(element =>
                string.Equals(
                    element.Name.LocalName,
                    "appSettings",
                    StringComparison.OrdinalIgnoreCase));

        var innovatorServerUrlElement = appSettings?
            .Elements()
            .FirstOrDefault(element =>
                string.Equals(
                    element.Name.LocalName,
                    "add",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    GetAttributeValue(element, "key"),
                    "InnovatorServerUrl",
                    StringComparison.OrdinalIgnoreCase));

        var appSettingsValue = GetAttributeValue(innovatorServerUrlElement, "value");
        if (!string.IsNullOrWhiteSpace(appSettingsValue))
        {
            return appSettingsValue.Trim();
        }

        var directElement = document
            .Descendants()
            .FirstOrDefault(element =>
                string.Equals(
                    element.Name.LocalName,
                    "InnovatorServerUrl",
                    StringComparison.OrdinalIgnoreCase));

        var directAttributeValue = GetAttributeValue(directElement, "value");
        if (!string.IsNullOrWhiteSpace(directAttributeValue))
        {
            return directAttributeValue.Trim();
        }

        var directText = directElement?.Value.Trim();
        return string.IsNullOrWhiteSpace(directText)
            ? null
            : directText;
    }

    private static string? GetAttributeValue(XElement? element, string attributeName)
    {
        return element?
            .Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(
                    attribute.Name.LocalName,
                    attributeName,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private string NormalizeServerPrefix(string innovatorServerUrl)
    {
        var value = innovatorServerUrl.Trim().TrimEnd('/');
        if (value.EndsWith(InnovatorSuffix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^InnovatorSuffix.Length].TrimEnd('/');
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"InnovatorServerUrl 無法轉成 Web URL：{innovatorServerUrl}");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !IsLocalLoopbackHost(uri.Host))
        {
            return value;
        }

        var machineName = _machineNameProvider();
        var converted = BuildUriWithReplacementHost(uri, machineName);
        _logger.Information(
            "Converted localhost InnovatorServerUrl host to machine name {MachineName}",
            machineName);
        return converted;
    }

    private static string BuildUriWithReplacementHost(Uri uri, string host)
    {
        var authority = uri.IsDefaultPort
            ? host
            : $"{host}:{uri.Port}";
        return $"{uri.Scheme}://{authority}{uri.PathAndQuery}".TrimEnd('/');
    }

    private static bool IsLocalLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) &&
            IPAddress.IsLoopback(address);
    }
}
