using System.IO;
using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class CmdVariableBuilder
{
    public IReadOnlyDictionary<string, string> Build(
        UpgradePathInfo paths,
        DatabaseConnectionOption connection,
        string innovatorServerConfigPath,
        string serverPrefix,
        string loginName,
        string password,
        string sqlLoginName = "sa",
        string sqlPassword = "",
        string copySourceDbName = "",
        IReadOnlyDictionary<string, string>? existingCmdVariables = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(connection);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in SupportPathRemapper.BuildExistingPathValues(
                     existingCmdVariables,
                     paths.SupportRoot))
        {
            values[pair.Key] = pair.Value;
        }

        values.Add("UPGRADE_DB_NAME", connection.Database);
        values.Add("COPY_SOURCE_DB_NAME", copySourceDbName);
        values.Add("COPY_TARGET_DB_NAME", connection.Database);
        values.Add("SOURCE_DB_SERV", connection.Server);
        values.Add("TARGET_DB_SERV", connection.Server);
        values.Add("SOURCE_SA_USER", sqlLoginName);
        values.Add("TARGET_SA_USER", sqlLoginName);
        values.Add("SOURCE_SA_PASS", sqlPassword);
        values.Add("TARGET_SA_PASS", sqlPassword);
        values.Add("INNOVATOR_SERVER_CONFIG", Path.GetFullPath(innovatorServerConfigPath));
        values.Add("AMLRUN_SERVERPREFIX", serverPrefix);
        values.Add("AMLRUN_DATABASE", "%UPGRADE_DB_NAME%");
        values.Add("AMLRUN_LOGINNAME", loginName);
        values.Add("AMLRUN_PASSWORD", password);
        return values;
    }
}
