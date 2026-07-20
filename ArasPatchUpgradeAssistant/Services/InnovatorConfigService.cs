using System.IO;
using System.Xml.Linq;
using ArasPatchUpgradeAssistant.Models;
using Serilog;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class InnovatorConfigService : IInnovatorConfigService
{
    private readonly IVaultConfigService _vaultConfigService;
    private readonly ILogger _logger;

    public InnovatorConfigService()
        : this(new VaultConfigService())
    {
    }

    public InnovatorConfigService(
        IVaultConfigService vaultConfigService,
        ILogger? logger = null)
    {
        _vaultConfigService = vaultConfigService;
        _logger = logger ?? Log.ForContext<InnovatorConfigService>();
    }

    public InnovatorConfiguration Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException(
                "請選擇 InnovatorServerConfig.xml。",
                nameof(configPath));
        }

        var fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"InnovatorServerConfig.xml 不存在：{fullPath}",
                fullPath);
        }

        var apServerRoot = Path.GetDirectoryName(fullPath)!;
        var connections = LoadConnections(fullPath);
        var vaultConfigPath = _vaultConfigService.GetVaultConfigPath(fullPath);
        var serverPrefix = _vaultConfigService.ParseInnovatorServerUrl(vaultConfigPath);

        return new InnovatorConfiguration(
            fullPath,
            apServerRoot,
            connections,
            serverPrefix,
            vaultConfigPath,
            serverPrefix);
    }

    private IReadOnlyList<DatabaseConnectionOption> LoadConnections(string configPath)
    {
        _logger.Information("Parse DB-Connection started {ConfigPath}", configPath);

        try
        {
            var connections = ReadConnections(XDocument.Load(configPath));
            if (connections.Count == 0)
            {
                throw new InvalidDataException(
                    "InnovatorServerConfig.xml 找不到有效的 DB-Connection，請確認節點包含 id 與 database。");
            }

            _logger.Information(
                "Parse DB-Connection completed {ConfigPath} {ConnectionCount} {HasServerValues}",
                configPath,
                connections.Count,
                connections.Any(connection => !string.IsNullOrWhiteSpace(connection.Server)));
            return connections;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Parse DB-Connection failed {ConfigPath}",
                configPath);
            throw;
        }
    }

    private static IReadOnlyList<DatabaseConnectionOption> ReadConnections(XDocument document)
    {
        return document
            .Descendants()
            .Where(element =>
                string.Equals(
                    element.Name.LocalName,
                    "DB-Connection",
                    StringComparison.OrdinalIgnoreCase))
            .Select(element => new
            {
                Label = GetAttributeValue(element, "id")?.Trim(),
                Database = GetAttributeValue(element, "database")?.Trim(),
                Server = GetAttributeValue(element, "server")?.Trim()
            })
            .Where(connection =>
                !string.IsNullOrWhiteSpace(connection.Label) &&
                !string.IsNullOrWhiteSpace(connection.Database))
            .Select(connection =>
                new DatabaseConnectionOption(
                    connection.Label!,
                    connection.Database!,
                    connection.Server ?? string.Empty))
            .ToArray();
    }

    private static string? GetAttributeValue(XElement element, string attributeName)
    {
        return element
            .Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(
                    attribute.Name.LocalName,
                    attributeName,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }
}
