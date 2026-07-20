using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArasPatchUpgradeAssistant.Models;
using Serilog;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class PatchExplanationService
{
    private const int BodyPreviewLineLimit = 20;
    private readonly ILogger _logger;
    private readonly AiPatchDescriptionService _aiPatchDescriptionService;
    private readonly AiPatchDescriptionSettingsService _aiSettingsService;
    private readonly ISecretProtectionService _secretProtectionService;

    public PatchExplanationService(
        ILogger? logger = null,
        AiPatchDescriptionService? aiPatchDescriptionService = null,
        AiPatchDescriptionSettingsService? aiSettingsService = null,
        ISecretProtectionService? secretProtectionService = null)
    {
        _logger = logger ?? Logger.None;
        _aiPatchDescriptionService = aiPatchDescriptionService ?? new AiPatchDescriptionService(_logger);
        _aiSettingsService = aiSettingsService ?? new AiPatchDescriptionSettingsService(logger: _logger);
        _secretProtectionService = secretProtectionService ?? new SecretProtectionService();
    }

    public PatchNoteGenerationResult Generate(PatchNoteGenerationRequest request)
    {
        return GenerateAsync(request).GetAwaiter().GetResult();
    }

    public async Task<PatchNoteGenerationResult> GenerateAsync(
        PatchNoteGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.Information(
            "Generate patch note started {BatFileName} {UpNumber}",
            request.BatFileName,
            request.UpNumber);

        var aiSettings = _aiSettingsService.Load();
        var patchXmlPath = ResolvePatchXmlPath(request);
        var patchXmlExists = !string.IsNullOrWhiteSpace(patchXmlPath) &&
            File.Exists(patchXmlPath);
        var analysis = patchXmlExists
            ? AnalyzePatchXml(patchXmlPath, Math.Max(aiSettings.MaxBodyPreviewLines, BodyPreviewLineLimit))
            : new PatchXmlAnalysisResult();
        var chineseDescription = await GenerateChineseDescriptionAsync(
                request,
                analysis,
                aiSettings,
                cancellationToken)
            .ConfigureAwait(false);
        var markdownPath = GetMarkdownPath(request);
        var warningMessage = patchXmlExists
            ? string.Empty
            : "Patch XML Missing";

        var generatedAt = DateTimeOffset.Now;
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        File.WriteAllText(
            markdownPath,
            BuildMarkdown(
                request,
                patchXmlPath,
                patchXmlExists,
                analysis,
                warningMessage,
                chineseDescription),
            Encoding.UTF8);

        _logger.Information(
            "Generate patch note completed {MarkdownPath} {PatchXmlExists}",
            markdownPath,
            patchXmlExists);

        return new PatchNoteGenerationResult
        {
            Succeeded = true,
            PatchXmlExists = patchXmlExists,
            MarkdownPath = markdownPath,
            PatchXmlPath = patchXmlPath,
            WarningMessage = warningMessage,
            IsAiGenerated = chineseDescription.IsAiGenerated,
            AiStatus = chineseDescription.Status,
            AiSourceMode = chineseDescription.SourceMode,
            AiErrorMessage = chineseDescription.ErrorMessage,
            GeneratedAt = generatedAt
        };
    }

    private async Task<PatchChineseDescriptionResult> GenerateChineseDescriptionAsync(
        PatchNoteGenerationRequest request,
        PatchXmlAnalysisResult analysis,
        AiPatchDescriptionSettings aiSettings,
        CancellationToken cancellationToken)
    {
        var apiKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(aiSettings.EncryptedOpenAiApiKey))
        {
            try
            {
                apiKey = _secretProtectionService.Unprotect(aiSettings.EncryptedOpenAiApiKey);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                _logger.Warning(
                    exception,
                    "AI patch description API key decryption failed");
                return PatchChineseDescriptionResult.Fallback(
                    aiSettings.OpenAiModel,
                    "Error",
                    "API Key 解密失敗");
            }
        }

        return await _aiPatchDescriptionService.GenerateAsync(
                new PatchDescriptionTranslationRequest
                {
                    UpNumber = request.UpNumber,
                    Name = request.Name,
                    Description = analysis.Description,
                    Type = analysis.Type,
                    BatType = request.BatType,
                    Order = request.Order,
                    Generation = request.Generation,
                    SoftwareVersion = request.SoftwareVersion,
                    DbTargetVersion = request.DbTargetVersion
                },
                aiSettings,
                apiKey,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private string ResolvePatchXmlPath(PatchNoteGenerationRequest request)
    {
        if (request.IsExternal &&
            string.Equals(
                Path.GetExtension(request.ExternalStoredFullPath),
                ".xml",
                StringComparison.OrdinalIgnoreCase) &&
            File.Exists(request.ExternalStoredFullPath))
        {
            return Path.GetFullPath(request.ExternalStoredFullPath);
        }

        var patchDirectory = ResolvePatchDirectory(request.PatchesBase, request.BatType);
        var patchFileName = $"{CleanDisplayName(request.Name)}.xml";
        return string.IsNullOrWhiteSpace(patchDirectory)
            ? patchFileName
            : Path.GetFullPath(Path.Combine(patchDirectory, patchFileName));
    }

    private static string ResolvePatchDirectory(string patchesBase, string batType)
    {
        var (domain, stage) = ParseBatType(batType);
        if (string.IsNullOrWhiteSpace(patchesBase) ||
            string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(stage))
        {
            return string.Empty;
        }

        var domainDirectory = FindDirectChildDirectory(
            patchesBase,
            domain.Equals("CORE", StringComparison.OrdinalIgnoreCase)
                ? name => name.Contains("core", StringComparison.OrdinalIgnoreCase)
                : name => name.Equals("PE", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("PE", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(domainDirectory))
        {
            domainDirectory = Path.Combine(
                patchesBase,
                domain.Equals("CORE", StringComparison.OrdinalIgnoreCase)
                    ? "core"
                    : "PE");
        }

        var stageName = stage.Equals("PRE", StringComparison.OrdinalIgnoreCase)
            ? "pre"
            : "post";
        var stageDirectory = FindDirectChildDirectory(
            domainDirectory,
            name => name.Equals(stageName, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(stageDirectory)
            ? Path.Combine(domainDirectory, stageName)
            : stageDirectory;
    }

    private static string? FindDirectChildDirectory(
        string parentDirectory,
        Func<string, bool> predicate)
    {
        try
        {
            if (!Directory.Exists(parentDirectory))
            {
                return null;
            }

            return Directory
                .EnumerateDirectories(parentDirectory)
                .FirstOrDefault(path => predicate(Path.GetFileName(path)));
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return null;
        }
    }

    private static (string Domain, string Stage) ParseBatType(string batType)
    {
        var tokens = batType
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var domain = tokens.Any(token => token.Equals("CORE", StringComparison.OrdinalIgnoreCase))
            ? "CORE"
            : tokens.Any(token => token.Equals("PE", StringComparison.OrdinalIgnoreCase))
                ? "PE"
                : string.Empty;
        var stage = tokens.Any(token => token.Equals("PRE", StringComparison.OrdinalIgnoreCase))
            ? "PRE"
            : tokens.Any(token => token.Equals("POST", StringComparison.OrdinalIgnoreCase))
                ? "POST"
                : string.Empty;

        return (domain, stage);
    }

    private PatchXmlAnalysisResult AnalyzePatchXml(
        string patchXmlPath,
        int maxBodyPreviewLines)
    {
        try
        {
            var document = XDocument.Load(patchXmlPath);
            var root = document.Root;
            var description = GetChildValue(root, "Description");
            var type = GetChildValue(root, "Type");
            var body = GetChildValue(root, "Body");
            var dataElement = root?.Elements().FirstOrDefault(element =>
                element.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase));
            var bodyLines = SplitLines(body);
            var sqlKeywords = FindSqlKeywords(body);

            return new PatchXmlAnalysisResult
            {
                Description = description,
                Type = type,
                HasBody = !string.IsNullOrWhiteSpace(body),
                HasData = dataElement is not null,
                BodyLineCount = string.IsNullOrWhiteSpace(body) ? 0 : bodyLines.Count,
                ContainsSql = ContainsSql(body, sqlKeywords),
                ContainsAml = ContainsAml(body),
                IsCSharp = type.Equals("CSharp", StringComparison.OrdinalIgnoreCase),
                SqlKeywords = sqlKeywords,
                PossibleItemTypes = FindPossibleItemTypes(body),
                PossibleTables = FindPossibleTables(body),
                PossibleMethods = FindPossibleMethods(body),
                BodyPreviewLines = bodyLines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(maxBodyPreviewLines)
                    .ToArray()
            };
        }
        catch (Exception exception) when (IsPathException(exception) ||
                                          exception is System.Xml.XmlException or InvalidOperationException)
        {
            _logger.Warning(
                exception,
                "Patch XML analysis failed {PatchXmlPath}",
                patchXmlPath);
            return new PatchXmlAnalysisResult
            {
                Description = $"Patch XML 解析失敗：{exception.Message}"
            };
        }
    }

    private string GetMarkdownPath(PatchNoteGenerationRequest request)
    {
        var assistantRoot = Path.Combine(
            request.SupportRoot,
            "UpgradeAssistant",
            request.CommandFolder);
        var batFolder = SanitizeFileName(Path.GetFileNameWithoutExtension(request.BatFileName));
        var fileNameSource = IsMeaningfulUpNumber(request.UpNumber)
            ? request.UpNumber
            : CleanDisplayName(request.Name);
        var fileName = $"{SanitizeFileName(fileNameSource)}.md";

        return Path.GetFullPath(Path.Combine(
            assistantRoot,
            "patch-notes",
            batFolder,
            fileName));
    }

    private static string BuildMarkdown(
        PatchNoteGenerationRequest request,
        string patchXmlPath,
        bool patchXmlExists,
        PatchXmlAnalysisResult analysis,
        string warningMessage,
        PatchChineseDescriptionResult chineseDescription)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Patch 說明 - {MarkdownText(request.UpNumber)} {MarkdownText(request.Name)}".TrimEnd());
        builder.AppendLine();
        builder.AppendLine("> 本文件由 Aras Innovator Patches 升級助手產生，只做靜態分析，未執行 Patch。");
        builder.AppendLine();

        if (!patchXmlExists)
        {
            builder.AppendLine($"**Warning:** {warningMessage}");
            builder.AppendLine();
        }

        builder.AppendLine("## 1. Patch 基本資訊");
        builder.AppendLine();
        builder.AppendLine("| 欄位 | 值 |");
        builder.AppendLine("| --- | --- |");
        AppendTableRow(builder, "UpNumber", request.UpNumber);
        AppendTableRow(builder, "Name", request.Name);
        AppendTableRow(builder, "BAT 檔名", request.BatFileName);
        AppendTableRow(builder, "BAT 類型", request.BatType);
        AppendTableRow(builder, "Catalog XML Path", request.CatalogXmlPath);
        AppendTableRow(builder, "Patch XML Path", patchXmlPath);
        AppendTableRow(builder, "Patch XML 狀態", patchXmlExists ? "Found" : "Missing");
        AppendTableRow(builder, "Order", request.Order);
        AppendTableRow(builder, "Generation", request.Generation);
        AppendTableRow(builder, "SoftwareVersion", request.SoftwareVersion);
        AppendTableRow(builder, "DbTargetVersion", request.DbTargetVersion);
        builder.AppendLine();

        builder.AppendLine("## 2. Patch 說明");
        builder.AppendLine();
        builder.AppendLine("### 原始 Description");
        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(analysis.Description)
            ? "待人工確認"
            : analysis.Description);
        builder.AppendLine();
        builder.AppendLine("### 中文說明");
        builder.AppendLine();
        builder.AppendLine(chineseDescription.ChineseExplanation);
        builder.AppendLine();
        builder.AppendLine("### 中文摘要");
        builder.AppendLine();
        builder.AppendLine(chineseDescription.ChineseSummary);
        builder.AppendLine();
        builder.AppendLine("### 影響範圍");
        builder.AppendLine();
        builder.AppendLine(chineseDescription.ImpactScope);
        builder.AppendLine();
        builder.AppendLine("### 風險提示");
        builder.AppendLine();
        builder.AppendLine(chineseDescription.RiskNotes);
        builder.AppendLine();
        builder.AppendLine("### AI 產生狀態");
        builder.AppendLine();
        builder.AppendLine("| 欄位 | 值 |");
        builder.AppendLine("| --- | --- |");
        AppendTableRow(builder, "是否使用 AI", ToYesNo(chineseDescription.IsAiGenerated));
        AppendTableRow(builder, "模型", chineseDescription.Model);
        AppendTableRow(builder, "說明來源", chineseDescription.SourceMode);
        AppendTableRow(builder, "是否傳送 Body", ToYesNo(chineseDescription.BodyIncluded));
        AppendTableRow(builder, "Description 字元數", chineseDescription.DescriptionChars.ToString(CultureInfo.InvariantCulture));
        AppendTableRow(builder, "Prompt 字元數", chineseDescription.PromptChars.ToString(CultureInfo.InvariantCulture));
        AppendTableRow(builder, "Request 狀態", chineseDescription.Status);
        AppendTableRow(builder, "錯誤代碼", chineseDescription.ErrorCode);
        AppendTableRow(builder, "狀態", chineseDescription.Status);
        AppendTableRow(builder, "訊息", chineseDescription.ErrorMessage);
        builder.AppendLine();

        builder.AppendLine("## 3. Patch XML 解析資訊");
        builder.AppendLine();
        builder.AppendLine("| 欄位 | 值 |");
        builder.AppendLine("| --- | --- |");
        AppendTableRow(builder, "Description", analysis.Description);
        AppendTableRow(builder, "Type", analysis.Type);
        AppendTableRow(builder, "是否有 Body", ToYesNo(analysis.HasBody));
        AppendTableRow(builder, "是否有 Data", ToYesNo(analysis.HasData));
        AppendTableRow(builder, "Body 行數", analysis.BodyLineCount.ToString(CultureInfo.InvariantCulture));
        AppendTableRow(builder, "是否包含 SQL", ToYesNo(analysis.ContainsSql));
        AppendTableRow(builder, "是否包含 AML", ToYesNo(analysis.ContainsAml));
        AppendTableRow(builder, "是否為 CSharp", ToYesNo(analysis.IsCSharp));
        builder.AppendLine();

        builder.AppendLine("## 4. 程式內容摘要");
        builder.AppendLine();
        if (!patchXmlExists)
        {
            builder.AppendLine("- 找不到 Patch XML，無法分析 Body。");
            builder.AppendLine("- 已記錄預期尋找路徑於 Patch XML Path。");
        }
        else if (!analysis.HasBody)
        {
            builder.AppendLine("- Patch XML 沒有 Body 內容。");
        }
        else
        {
            builder.AppendLine(analysis.IsCSharp
                ? "- Type=CSharp，以下為 Body 的靜態關鍵字摘要。"
                : "- 以下為 Body 的靜態關鍵字摘要。");
            builder.AppendLine($"- SQL 關鍵字：{FormatList(analysis.SqlKeywords)}");
            builder.AppendLine($"- 可能異動 ItemType：{FormatList(analysis.PossibleItemTypes)}");
            builder.AppendLine($"- 可能異動 Table：{FormatList(analysis.PossibleTables)}");
            builder.AppendLine($"- 可能異動 Method：{FormatList(analysis.PossibleMethods)}");

            if (analysis.SqlKeywords.Contains("applySQL", StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine("- **包含 SQL 執行：Body 內偵測到 applySQL。**");
            }

            var mutationKeywords = analysis.SqlKeywords
                .Where(keyword => keyword is "UPDATE" or "INSERT" or "DELETE")
                .ToArray();
            if (mutationKeywords.Length > 0)
            {
                builder.AppendLine($"- 可能異動類型：{string.Join(", ", mutationKeywords)}");
            }
        }
        builder.AppendLine();

        builder.AppendLine("## 5. 風險提示");
        builder.AppendLine();
        builder.AppendLine("- 僅為靜態分析。");
        builder.AppendLine("- 未執行 Patch。");
        builder.AppendLine("- 此中文說明僅依 Patch Description 產生，未分析完整 Patch Body。實際影響仍需工程師確認 Patch XML / Body。");
        builder.AppendLine("- 實際影響仍需由工程師確認。");
        builder.AppendLine();

        builder.AppendLine("## 6. 原始 Body 摘要");
        builder.AppendLine();
        if (analysis.BodyPreviewLines.Count == 0)
        {
            builder.AppendLine("_沒有可顯示的 Body 摘要。_");
        }
        else
        {
            builder.AppendLine("```text");
            foreach (var line in analysis.BodyPreviewLines.Take(BodyPreviewLineLimit))
            {
                builder.AppendLine(line);
            }
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine($"_僅顯示前 {BodyPreviewLineLimit} 行非空白內容，未完整輸出 Body。_");
        }

        return builder.ToString();
    }

    private static ChineseDescription BuildChineseDescription(string originalDescription)
    {
        var description = MarkdownText(originalDescription);
        if (string.IsNullOrWhiteSpace(description))
        {
            return ChineseDescription.NeedsReview;
        }

        var normalized = Regex.Replace(description, @"\s+", " ").Trim();
        var clauses = Regex
            .Split(normalized, @"(?<=[.;])\s+")
            .Select(value => value.Trim(' ', '.', ';'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var translatedClauses = clauses
            .Select(TryTranslateDescriptionClause)
            .ToArray();

        if (translatedClauses.Length == 0 ||
            translatedClauses.Any(value => string.IsNullOrWhiteSpace(value)))
        {
            return ChineseDescription.NeedsReview;
        }

        var explanation = translatedClauses.Length <= 2
            ? string.Join(Environment.NewLine + Environment.NewLine, translatedClauses)
            : string.Join(
                Environment.NewLine,
                translatedClauses.Select(value => $"- {value}"));

        var summary = BuildChineseSummary(translatedClauses, normalized);
        return string.IsNullOrWhiteSpace(summary)
            ? ChineseDescription.NeedsReview
            : new ChineseDescription(explanation, summary);
    }

    private static string TryTranslateDescriptionClause(string clause)
    {
        var value = clause.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var upperValue = value.ToUpperInvariant();
        var knownVerb = upperValue.Contains("FIX", StringComparison.Ordinal) ||
            upperValue.Contains("DELETE", StringComparison.Ordinal) ||
            upperValue.Contains("REMOVE", StringComparison.Ordinal) ||
            upperValue.Contains("ADD", StringComparison.Ordinal) ||
            upperValue.Contains("UPDATE", StringComparison.Ordinal) ||
            upperValue.Contains("CHANGE", StringComparison.Ordinal) ||
            upperValue.Contains("REPLACE", StringComparison.Ordinal) ||
            upperValue.Contains("USE", StringComparison.Ordinal) ||
            upperValue.Contains("SUPPORT", StringComparison.Ordinal) ||
            upperValue.Contains("ENABLE", StringComparison.Ordinal) ||
            upperValue.Contains("DISABLE", StringComparison.Ordinal) ||
            upperValue.Contains("IMPROVE", StringComparison.Ordinal) ||
            upperValue.Contains("SET", StringComparison.Ordinal) ||
            upperValue.Contains("CORRECT", StringComparison.Ordinal);
        if (!knownVerb)
        {
            return string.Empty;
        }

        var translated = value;
        foreach (var replacement in DescriptionTermReplacements)
        {
            translated = Regex.Replace(
                translated,
                replacement.Pattern,
                replacement.Replacement,
                RegexOptions.IgnoreCase);
        }

        translated = Regex.Replace(translated, @"\s+", " ").Trim();
        return translated.EndsWith('。')
            ? translated
            : $"{translated}。";
    }

    private static string BuildChineseSummary(
        IReadOnlyList<string> translatedClauses,
        string originalDescription)
    {
        if (translatedClauses.Count == 0)
        {
            return string.Empty;
        }

        var firstClause = translatedClauses[0].TrimEnd('。');
        var purpose = originalDescription.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
            originalDescription.Contains("correct", StringComparison.OrdinalIgnoreCase)
                ? "此 Patch 主要用於修正既有功能或資料的不正確行為"
                : originalDescription.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                  originalDescription.Contains("remove", StringComparison.OrdinalIgnoreCase)
                    ? "此 Patch 主要用於移除或清理指定的設定、屬性或資料"
                    : originalDescription.Contains("add", StringComparison.OrdinalIgnoreCase) ||
                      originalDescription.Contains("support", StringComparison.OrdinalIgnoreCase) ||
                      originalDescription.Contains("enable", StringComparison.OrdinalIgnoreCase)
                        ? "此 Patch 主要用於新增或啟用指定能力"
                        : originalDescription.Contains("update", StringComparison.OrdinalIgnoreCase) ||
                          originalDescription.Contains("change", StringComparison.OrdinalIgnoreCase) ||
                          originalDescription.Contains("replace", StringComparison.OrdinalIgnoreCase)
                            ? "此 Patch 主要用於調整既有設定或程式邏輯"
                            : string.Empty;

        if (string.IsNullOrWhiteSpace(purpose))
        {
            return string.Empty;
        }

        return $"{purpose}。重點為：{firstClause}。";
    }

    private static readonly IReadOnlyList<(string Pattern, string Replacement)> DescriptionTermReplacements =
    [
        (@"\bfix(?:es|ed|ing)?\b", "修正"),
        (@"\bcorrect(?:s|ed|ing)?\b", "修正"),
        (@"\bdelete(?:s|d|ing)?\b", "刪除"),
        (@"\bremove(?:s|d|ing)?\b", "移除"),
        (@"\badd(?:s|ed|ing)?\b", "新增"),
        (@"\bupdate(?:s|d|ing)?\b", "更新"),
        (@"\bchange(?:s|d|ing)?\b", "調整"),
        (@"\breplace(?:s|d|ing)?\b", "取代"),
        (@"\buse(?:s|d|ing)?\b", "使用"),
        (@"\bsupport(?:s|ed|ing)?\b", "支援"),
        (@"\benable(?:s|d|ing)?\b", "啟用"),
        (@"\bdisable(?:s|d|ing)?\b", "停用"),
        (@"\bimprove(?:s|d|ing|ment)?\b", "改善"),
        (@"\bissue\b", "問題"),
        (@"\bissues\b", "問題"),
        (@"\bproblem\b", "問題"),
        (@"\bproblems\b", "問題"),
        (@"\berror\b", "錯誤"),
        (@"\berrors\b", "錯誤"),
        (@"\bexception\b", "例外"),
        (@"\bexceptions\b", "例外"),
        (@"\bmissing\b", "缺少"),
        (@"\binvalid\b", "無效"),
        (@"\bincorrect\b", "不正確"),
        (@"\bset(?:s|ting)?\b", "設定"),
        (@"\bproperty\b", "屬性"),
        (@"\bproperties\b", "屬性"),
        (@"\bmethod\b", "方法"),
        (@"\bmethods\b", "方法"),
        (@"\bform\b", "表單"),
        (@"\bforms\b", "表單"),
        (@"\bitem type\b", "ItemType"),
        (@"\bitem types\b", "ItemType"),
        (@"\brelationship\b", "關聯"),
        (@"\brelationships\b", "關聯"),
        (@"\bpermission\b", "權限"),
        (@"\bpermissions\b", "權限"),
        (@"\bworkflow\b", "流程"),
        (@"\bworkflows\b", "流程"),
        (@"\blifecycle\b", "生命週期"),
        (@"\bclassification\b", "分類"),
        (@"\bcontainer\b", "容器"),
        (@"\bserver\b", "伺服器"),
        (@"\bclient\b", "客戶端"),
        (@"\bdatabase\b", "資料庫"),
        (@"\btable\b", "資料表"),
        (@"\btables\b", "資料表"),
        (@"\bcolumn\b", "欄位"),
        (@"\bcolumns\b", "欄位"),
        (@"\bindex\b", "索引"),
        (@"\bindexes\b", "索引"),
        (@"\bhtml_code\b", "html_code"),
        (@"\bCore\b", "Core"),
        (@"\bPE\b", "PE"),
        (@"\bIoC\b", "IoC"),
        (@"\bAML\b", "AML"),
        (@"\bSQL\b", "SQL"),
        (@"\bin\b", "在"),
        (@"\bfrom\b", "從"),
        (@"\bto\b", "到"),
        (@"\bwith\b", "並搭配"),
        (@"\bfor\b", "針對"),
        (@"\bwhen\b", "當"),
        (@"\bwhere\b", "在下列情境"),
        (@"\bthat\b", ""),
        (@"\bof\b", "的"),
        (@"\band\b", "與"),
        (@"\bor\b", "或"),
        (@"\bthe\b", ""),
        (@"\ba\b", ""),
        (@"\ban\b", "")
    ];

    private sealed record ChineseDescription(string Explanation, string Summary)
    {
        public static ChineseDescription NeedsReview { get; } =
            new("待人工確認", "待人工確認");
    }

    private static IReadOnlyList<string> SplitLines(string value) =>
        value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static string GetChildValue(XElement? root, string childName) =>
        root?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName.Equals(
                childName,
                StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim() ?? string.Empty;

    private static IReadOnlyList<string> FindSqlKeywords(string body)
    {
        string[] candidates =
        [
            "applySQL",
            "UPDATE",
            "INSERT",
            "DELETE",
            "SELECT",
            "ALTER",
            "CREATE",
            "DROP",
            "EXEC"
        ];

        return candidates
            .Where(keyword => Regex.IsMatch(
                body,
                keyword.Equals("applySQL", StringComparison.OrdinalIgnoreCase)
                    ? @"\bapplySQL\b"
                    : $@"\b{keyword}\b",
                RegexOptions.IgnoreCase))
            .ToArray();
    }

    private static bool ContainsSql(string body, IReadOnlyCollection<string> sqlKeywords) =>
        sqlKeywords.Count > 0 ||
        Regex.IsMatch(body, @"\bSQL\b", RegexOptions.IgnoreCase);

    private static bool ContainsAml(string body) =>
        Regex.IsMatch(body, @"<\s*AML\b|<\s*Item\b|\bapplyAML\b", RegexOptions.IgnoreCase);

    private static IReadOnlyList<string> FindPossibleItemTypes(string body)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(
                     body,
                     @"<\s*Item\b[^>]*\btype\s*=\s*[""'](?<value>[^""']+)[""']",
                     RegexOptions.IgnoreCase))
        {
            values.Add(match.Groups["value"].Value);
        }

        foreach (Match match in Regex.Matches(
                     body,
                     @"getItemTypeByName\s*\(\s*[""'](?<value>[^""']+)[""']",
                     RegexOptions.IgnoreCase))
        {
            values.Add(match.Groups["value"].Value);
        }

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static IReadOnlyList<string> FindPossibleTables(string body)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(
                     body,
                     @"\b(?:UPDATE|INTO|FROM|JOIN)\s+\[?(?<value>[A-Za-z0-9_\.]+)\]?",
                     RegexOptions.IgnoreCase))
        {
            var value = match.Groups["value"].Value;
            if (!SqlStopWords.Contains(value))
            {
                values.Add(value);
            }
        }

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static IReadOnlyList<string> FindPossibleMethods(string body)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(
                     body,
                     @"applyMethod\s*\(\s*[""'](?<value>[^""']+)[""']",
                     RegexOptions.IgnoreCase))
        {
            values.Add(match.Groups["value"].Value);
        }

        foreach (Match match in Regex.Matches(
                     body,
                     @"<\s*Item\b[^>]*\btype\s*=\s*[""']Method[""'][\s\S]*?<\s*name\s*>\s*(?<value>[^<]+)\s*<\s*/\s*name\s*>",
                     RegexOptions.IgnoreCase))
        {
            values.Add(match.Groups["value"].Value);
        }

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static readonly HashSet<string> SqlStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "SET",
        "WHERE",
        "VALUES"
    };

    private static void AppendTableRow(StringBuilder builder, string name, string value) =>
        builder.AppendLine($"| {MarkdownTableText(name)} | {MarkdownTableText(value)} |");

    private static string FormatList(IReadOnlyCollection<string> values) =>
        values.Count == 0
            ? "未偵測到"
            : string.Join(", ", values.Select(MarkdownText));

    private static string ToYesNo(bool value) => value ? "是" : "否";

    private static string MarkdownTableText(string value) =>
        MarkdownText(value).Replace("|", "\\|", StringComparison.Ordinal);

    private static string MarkdownText(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? ""
            : value
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();

    private static string CleanDisplayName(string value) =>
        (value ?? string.Empty)
            .Replace("🛠", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static bool IsMeaningfulUpNumber(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Equals("Custom", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string value)
    {
        var cleaned = CleanDisplayName(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "patch-note";
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalidChar, '_');
        }

        return cleaned.Length <= 120
            ? cleaned
            : cleaned[..120];
    }

    private static bool IsPathException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or ArgumentException
            or NotSupportedException;
}
