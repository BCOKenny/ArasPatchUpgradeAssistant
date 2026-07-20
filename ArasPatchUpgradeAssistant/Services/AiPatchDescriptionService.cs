using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArasPatchUpgradeAssistant.Models;
using Serilog;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class AiPatchDescriptionService
{
    private const string SourceMode = "DescriptionOnly";
    private const bool BodyIncluded = false;
    private const int BodyChars = 0;
    private const int BodyPreviewLines = 0;
    private const double Temperature = 0.1;
    private const int PromptPreviewLimit = 1500;
    private const int ResponsePreviewLimit = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ILogger _logger;

    public AiPatchDescriptionService(ILogger? logger = null)
    {
        _logger = logger ?? Logger.None;
    }

    public async Task<PatchChineseDescriptionResult> GenerateAsync(
        PatchDescriptionTranslationRequest request,
        AiPatchDescriptionSettings settings,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.EnableAiPatchDescription)
        {
            return PatchChineseDescriptionResult.Fallback(
                settings.OpenAiModel,
                "Fallback",
                "AI Patch 中文說明未啟用",
                sourceMode: SourceMode,
                descriptionChars: request.Description.Length);
        }

        if (string.IsNullOrWhiteSpace(settings.OpenAiBaseUrl) ||
            string.IsNullOrWhiteSpace(settings.OpenAiModel))
        {
            _logger.Warning(
                "AI patch description settings incomplete {OpenAiBaseUrl} {OpenAiModel}",
                settings.OpenAiBaseUrl,
                settings.OpenAiModel);
            return PatchChineseDescriptionResult.Fallback(
                settings.OpenAiModel,
                "Fallback",
                "AI 設定不完整",
                sourceMode: SourceMode,
                descriptionChars: request.Description.Length);
        }

        try
        {
            var endpoint = BuildChatCompletionsEndpoint(settings.OpenAiBaseUrl);
            var prompt = BuildUserPrompt(request);
            var requestBody = BuildRequestBody(settings, prompt);
            var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
            var endpointPath = GetEndpointPath(endpoint);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds)
            };
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                endpoint);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
            }

            httpRequest.Content = new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json");

            _logger.Information(
                "AI patch description request started {BaseUrl} {Model} {EndpointPath} {EnableAiPatchDescription} {SourceMode} {UpNumber} {PatchName} {DescriptionChars} {BodyChars} {BodyIncluded} {BodyPreviewLines} {PromptChars} {RequestJsonChars} {MaxTokens} {Temperature}",
                settings.OpenAiBaseUrl,
                settings.OpenAiModel,
                endpointPath,
                settings.EnableAiPatchDescription,
                SourceMode,
                request.UpNumber,
                request.Name,
                request.Description.Length,
                BodyChars,
                BodyIncluded,
                BodyPreviewLines,
                prompt.Length,
                requestJson.Length,
                null,
                Temperature);

            if (settings.EnableAiRequestDebugLog)
            {
                var promptPreview = Truncate(prompt, PromptPreviewLimit);
                _logger.Information(
                    "AI patch description prompt preview {SourceMode} {PromptPreviewChars} {PromptPreview}",
                    SourceMode,
                    promptPreview.Length,
                    promptPreview);
            }

            using var response = await httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);
            var responseText = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            LogRateLimitHeaders(response);

            if (!response.IsSuccessStatusCode)
            {
                var error = ParseOpenAiError(responseText);
                var fallbackReason = string.IsNullOrWhiteSpace(error.Message)
                    ? $"API 回傳 HTTP {(int)response.StatusCode}"
                    : error.Message;
                _logger.Warning(
                    "AI patch description request failed {StatusCode} {OpenAiModel} {ErrorMessage} {ErrorType} {ErrorParam} {ErrorCode} {FallbackReason} {ResponseBodyPreview}",
                    (int)response.StatusCode,
                    settings.OpenAiModel,
                    error.Message,
                    error.Type,
                    error.Param,
                    error.Code,
                    fallbackReason,
                    error.IsJson
                        ? string.Empty
                        : Truncate(responseText, ResponsePreviewLimit));
                return PatchChineseDescriptionResult.Fallback(
                    settings.OpenAiModel,
                    "Error",
                    fallbackReason,
                    errorCode: string.IsNullOrWhiteSpace(error.Code)
                        ? ((int)response.StatusCode).ToString()
                        : error.Code,
                    sourceMode: SourceMode,
                    descriptionChars: request.Description.Length,
                    promptChars: prompt.Length,
                    requestJsonChars: requestJson.Length);
            }

            var content = ExtractAssistantContent(responseText);
            var result = ParseStructuredResult(
                content,
                settings.OpenAiModel,
                request.Description.Length,
                prompt.Length,
                requestJson.Length);
            if (!result.IsAiGenerated)
            {
                _logger.Warning(
                    "AI patch description response format invalid {OpenAiModel} {ResponseLength}",
                    settings.OpenAiModel,
                    responseText.Length);
            }

            return result;
        }
        catch (TaskCanceledException exception)
        {
            _logger.Warning(
                exception,
                "AI patch description request timeout {OpenAiModel}",
                settings.OpenAiModel);
            return PatchChineseDescriptionResult.Fallback(
                settings.OpenAiModel,
                "Error",
                "API 呼叫逾時",
                errorCode: "timeout",
                sourceMode: SourceMode,
                descriptionChars: request.Description.Length);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
                   or InvalidOperationException or UriFormatException or KeyNotFoundException)
        {
            _logger.Warning(
                exception,
                "AI patch description request failed {OpenAiModel}",
                settings.OpenAiModel);
            return PatchChineseDescriptionResult.Fallback(
                settings.OpenAiModel,
                "Error",
                exception.Message,
                errorCode: exception.GetType().Name,
                sourceMode: SourceMode,
                descriptionChars: request.Description.Length);
        }
    }

    private static string BuildChatCompletionsEndpoint(string baseUrl) =>
        $"{baseUrl.TrimEnd('/')}/chat/completions";

    private static object BuildRequestBody(
        AiPatchDescriptionSettings settings,
        string prompt) => new
        {
            model = settings.OpenAiModel,
            temperature = Temperature,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                        你是 Aras Innovator 升級工程師。請只根據使用者提供的 Patch Description 與少量 metadata，產生繁體中文說明。
                        不要假設、推測或編造未提供的 Patch Body、CSharp、SQL、AML 或實際程式細節。
                        請保持保守；若不確定，請標示「待工程師確認」。
                        請只輸出 JSON，不要使用 Markdown，不要加上額外說明。
                        JSON 欄位必須是 chineseExplanation、chineseSummary、impactScope、riskNotes。
                        """
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

    private static string BuildUserPrompt(PatchDescriptionTranslationRequest request)
    {
        return $$"""
            請為以下 Aras Innovator Patch 產生繁體中文說明。
            注意：本次只提供 Patch Description 與 metadata，未提供完整 Patch Body。
            請不要編造未提供的 Body、CSharp、SQL、AML 或實際程式細節。

            UpNumber: {{request.UpNumber}}
            Name: {{request.Name}}
            Type: {{request.Type}}
            BAT 類型: {{request.BatType}}
            Order: {{request.Order}}
            Generation: {{request.Generation}}
            SoftwareVersion: {{request.SoftwareVersion}}
            DbTargetVersion: {{request.DbTargetVersion}}

            原始 Description:
            {{request.Description}}

            請輸出 JSON：
            {
              "chineseExplanation": "中文說明，若資訊不足請寫待工程師確認",
              "chineseSummary": "1 到 3 句中文摘要，若資訊不足請寫待工程師確認",
              "impactScope": "影響範圍，若資訊不足請寫待工程師確認",
              "riskNotes": "風險提示，若資訊不足請寫待工程師確認"
            }
            """;
    }

    private static string ExtractAssistantContent(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        return document
            .RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static PatchChineseDescriptionResult ParseStructuredResult(
        string content,
        string model,
        int descriptionChars,
        int promptChars,
        int requestJsonChars)
    {
        try
        {
            var json = StripCodeFence(content);
            var node = JsonNode.Parse(json);
            var explanation = node?["chineseExplanation"]?.GetValue<string>() ?? string.Empty;
            var summary = node?["chineseSummary"]?.GetValue<string>() ?? string.Empty;
            var impactScope = node?["impactScope"]?.GetValue<string>() ?? string.Empty;
            var riskNotes = node?["riskNotes"]?.GetValue<string>() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(explanation) ||
                string.IsNullOrWhiteSpace(summary) ||
                string.IsNullOrWhiteSpace(impactScope) ||
                string.IsNullOrWhiteSpace(riskNotes))
            {
                return PatchChineseDescriptionResult.Fallback(
                    model,
                    "Fallback",
                    "AI 回應格式不完整",
                    errorCode: "invalid_response",
                    sourceMode: SourceMode,
                    descriptionChars: descriptionChars,
                    promptChars: promptChars,
                    requestJsonChars: requestJsonChars);
            }

            return new PatchChineseDescriptionResult
            {
                ChineseExplanation = explanation.Trim(),
                ChineseSummary = summary.Trim(),
                ImpactScope = impactScope.Trim(),
                RiskNotes = riskNotes.Trim(),
                IsAiGenerated = true,
                Model = model,
                Status = "Success",
                SourceMode = SourceMode,
                BodyIncluded = BodyIncluded,
                DescriptionChars = descriptionChars,
                BodyChars = BodyChars,
                BodyPreviewLines = BodyPreviewLines,
                PromptChars = promptChars,
                RequestJsonChars = requestJsonChars
            };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return PatchChineseDescriptionResult.Fallback(
                model,
                "Fallback",
                "AI 回應格式錯誤",
                errorCode: "invalid_json",
                sourceMode: SourceMode,
                descriptionChars: descriptionChars,
                promptChars: promptChars,
                requestJsonChars: requestJsonChars);
        }
    }

    private static string StripCodeFence(string content)
    {
        var value = content.Trim();
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var firstLineBreak = value.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineBreak >= 0 && lastFence > firstLineBreak
            ? value[(firstLineBreak + 1)..lastFence].Trim()
            : value;
    }

    private void LogRateLimitHeaders(HttpResponseMessage response)
    {
        var requestId = GetHeaderValue(response, "x-request-id");
        var retryAfter = GetHeaderValue(response, "retry-after");
        var requestLimit = GetHeaderValue(response, "x-ratelimit-limit-requests");
        var requestRemaining = GetHeaderValue(response, "x-ratelimit-remaining-requests");
        var requestReset = GetHeaderValue(response, "x-ratelimit-reset-requests");
        var tokenLimit = GetHeaderValue(response, "x-ratelimit-limit-tokens");
        var tokenRemaining = GetHeaderValue(response, "x-ratelimit-remaining-tokens");
        var tokenReset = GetHeaderValue(response, "x-ratelimit-reset-tokens");

        if (string.IsNullOrWhiteSpace(requestId) &&
            string.IsNullOrWhiteSpace(retryAfter) &&
            string.IsNullOrWhiteSpace(requestLimit) &&
            string.IsNullOrWhiteSpace(requestRemaining) &&
            string.IsNullOrWhiteSpace(requestReset) &&
            string.IsNullOrWhiteSpace(tokenLimit) &&
            string.IsNullOrWhiteSpace(tokenRemaining) &&
            string.IsNullOrWhiteSpace(tokenReset))
        {
            return;
        }

        _logger.Information(
            "AI patch description response headers {RequestId} {RetryAfter} {RateLimitLimitRequests} {RateLimitRemainingRequests} {RateLimitResetRequests} {RateLimitLimitTokens} {RateLimitRemainingTokens} {RateLimitResetTokens}",
            requestId,
            retryAfter,
            requestLimit,
            requestRemaining,
            requestReset,
            tokenLimit,
            tokenRemaining,
            tokenReset);
    }

    private static OpenAiErrorDetail ParseOpenAiError(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return new OpenAiErrorDetail(IsJson: true);
            }

            return new OpenAiErrorDetail(
                GetJsonString(error, "message"),
                GetJsonString(error, "type"),
                GetJsonString(error, "param"),
                GetJsonString(error, "code"),
                IsJson: true);
        }
        catch (JsonException)
        {
            return new OpenAiErrorDetail(IsJson: false);
        }
    }

    private static string GetEndpointPath(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri.PathAndQuery
            : endpoint;
    }

    private static string GetHeaderValue(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values) ||
            response.Content.Headers.TryGetValues(name, out values))
        {
            return string.Join(",", values);
        }

        return string.Empty;
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? property.ToString()
                : string.Empty;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed record OpenAiErrorDetail(
        string Message = "",
        string Type = "",
        string Param = "",
        string Code = "",
        bool IsJson = false);

}
