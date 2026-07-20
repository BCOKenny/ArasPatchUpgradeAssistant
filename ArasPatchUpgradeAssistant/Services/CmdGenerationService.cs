using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public sealed partial class CmdGenerationService : ICmdGenerationService
{
    public CmdGenerationResult Generate(
        string sourcePath,
        string machineName,
        IReadOnlyDictionary<string, string> values)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"來源 SETUP CMD 不存在：{sourcePath}", sourcePath);
        }

        if (string.IsNullOrWhiteSpace(machineName) ||
            machineName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("機器名稱無法用於 SETUP CMD 檔名。", nameof(machineName));
        }

        var sourceFullPath = Path.GetFullPath(sourcePath);
        var targetPath = Path.Combine(
            Path.GetDirectoryName(sourceFullPath)!,
            $"SETUP-DEFAULTS-{machineName}.CMD");

        if (string.Equals(sourceFullPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("目標檔不可與來源 SETUP CMD 相同。");
        }

        var sourceBytes = File.ReadAllBytes(sourceFullPath);
        var encodingInfo = DetectEncoding(sourceBytes);
        var content = encodingInfo.Encoding.GetString(
            sourceBytes,
            encodingInfo.PreambleLength,
            sourceBytes.Length - encodingInfo.PreambleLength);
        var transformed = Transform(content, values);

        var output = encodingInfo.Encoding.GetBytes(transformed.Content);
        if (encodingInfo.PreambleLength > 0)
        {
            output = encodingInfo.Encoding.GetPreamble().Concat(output).ToArray();
        }

        File.WriteAllBytes(targetPath, output);
        return new CmdGenerationResult(targetPath, transformed.Changes);
    }

    public static CmdTransformResult Transform(
        string source,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(values);

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changes = new List<CmdVariableChange>();

        for (var index = 0; index < lines.Count; index++)
        {
            var match = SetLinePattern().Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            if (!values.TryGetValue(name, out var newValue))
            {
                continue;
            }

            var quote = match.Groups["quote"].Value;
            var rest = match.Groups["rest"].Value;
            var (oldValue, valuePrefix, suffix) = SplitValueAndSuffix(
                rest,
                quote.Length > 0);
            lines[index] =
                match.Groups["before"].Value +
                quote +
                name +
                match.Groups["separator"].Value +
                valuePrefix +
                newValue +
                suffix;

            if (matched.Add(name))
            {
                changes.Add(new CmdVariableChange(name, "更新", oldValue, newValue));
            }
        }

        foreach (var pair in values)
        {
            if (matched.Contains(pair.Key) ||
                SupportPathRemapper.IsSupportedVariable(pair.Key))
            {
                continue;
            }

            lines.Add($"@SET {pair.Key}={pair.Value}");
            changes.Add(new CmdVariableChange(pair.Key, "新增", null, pair.Value));
        }

        return new CmdTransformResult(
            string.Join("\r\n", lines) + "\r\n",
            changes);
    }

    public static IReadOnlyDictionary<string, string> ExtractSetVariables(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in normalized.Split('\n'))
        {
            var match = SetLinePattern().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            var quote = match.Groups["quote"].Value;
            var rest = match.Groups["rest"].Value;
            var (value, _, _) = SplitValueAndSuffix(rest, quote.Length > 0);
            values[name] = value;
        }

        return values;
    }

    private static (string Value, string ValuePrefix, string Suffix) SplitValueAndSuffix(
        string rest,
        bool quotedAssignment)
    {
        if (quotedAssignment)
        {
            var closingQuote = rest.IndexOf('"');
            return closingQuote >= 0
                ? (rest[..closingQuote], string.Empty, rest[closingQuote..])
                : (rest, string.Empty, "\"");
        }

        if (rest.StartsWith('"'))
        {
            var closingQuote = rest.IndexOf('"', 1);
            return closingQuote >= 0
                ? (rest[1..closingQuote], "\"", rest[closingQuote..])
                : (rest[1..], "\"", "\"");
        }

        var comment = InlineCommentPattern().Match(rest);
        return comment.Success
            ? (rest[..comment.Index], string.Empty, rest[comment.Index..])
            : (rest, string.Empty, string.Empty);
    }

    private static EncodingInfo DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return new EncodingInfo(new UTF8Encoding(true), Encoding.UTF8.Preamble.Length);
        }

        if (bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble))
        {
            return new EncodingInfo(new UnicodeEncoding(false, true), Encoding.Unicode.Preamble.Length);
        }

        if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            return new EncodingInfo(
                new UnicodeEncoding(true, true),
                Encoding.BigEndianUnicode.Preamble.Length);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansiCodePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
        return new EncodingInfo(Encoding.GetEncoding(ansiCodePage), 0);
    }

    private sealed record EncodingInfo(Encoding Encoding, int PreambleLength);

    [GeneratedRegex(
        @"^(?<before>\s*@SET\s+)(?<quote>"")?(?<name>[A-Za-z0-9_]+)(?<separator>\s*=\s*)(?<rest>.*)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex SetLinePattern();

    [GeneratedRegex(@"\s+(?:(?:&\s*)?(?:REM\b|::))", RegexOptions.IgnoreCase)]
    private static partial Regex InlineCommentPattern();
}
