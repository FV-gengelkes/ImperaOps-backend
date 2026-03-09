using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Ai;

public sealed class ClaudeService : IClaudeService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<ClaudeService> _logger;

    public ClaudeService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ClaudeService> logger)
    {
        _httpFactory = httpFactory;
        _apiKey = config["Claude:ApiKey"] ?? "";
        _model = config["Claude:Model"] ?? "claude-sonnet-4-20250514";
        _logger = logger;
    }

    public async Task<AiCategorizationResult> CategorizeAsync(
        string title,
        string description,
        IReadOnlyList<NamedItem> eventTypes,
        IReadOnlyList<NamedItem> rootCauses,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return new AiCategorizationResult(null, null, 0, null, null, 0, "AI service not configured.");

        var typeList = string.Join("\n", eventTypes.Select(t => $"- id:{t.Id} name:\"{t.Name}\""));
        var rcList = rootCauses.Count > 0
            ? string.Join("\n", rootCauses.Select(r => $"- id:{r.Id} name:\"{r.Name}\""))
            : "None defined";

        var system = @"You are an incident/event management AI assistant. Given an event title and description, suggest the most appropriate event type and root cause from the provided lists. Respond with valid JSON only, no markdown.

JSON schema:
{""eventTypeId"":number|null,""eventTypeName"":string|null,""eventTypeConfidence"":number(0-1),""rootCauseId"":number|null,""rootCauseName"":string|null,""rootCauseConfidence"":number(0-1),""reasoning"":string}";

        var prompt = $"Event Title: {title}\nDescription: {description}\n\nAvailable Event Types:\n{typeList}\n\nAvailable Root Causes:\n{rcList}";

        var json = await CallClaudeAsync(system, prompt, ct);
        if (json == null)
            return new AiCategorizationResult(null, null, 0, null, null, 0, "AI request failed.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new AiCategorizationResult(
                GetNullableLong(root, "eventTypeId"),
                GetNullableString(root, "eventTypeName"),
                GetDouble(root, "eventTypeConfidence"),
                GetNullableLong(root, "rootCauseId"),
                GetNullableString(root, "rootCauseName"),
                GetDouble(root, "rootCauseConfidence"),
                GetNullableString(root, "reasoning") ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI categorization response");
            return new AiCategorizationResult(null, null, 0, null, null, 0, "Failed to parse AI response.");
        }
    }

    public async Task<AiInvestigationResult> SuggestInvestigationAsync(
        string eventTitle,
        string description,
        string? location,
        string? summary,
        IReadOnlyList<string> witnessStatements,
        IReadOnlyList<string> evidenceDescriptions,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return new AiInvestigationResult(null, null, "AI service not configured.");

        var system = @"You are an incident investigation AI assistant. Based on the event details, witness statements, and evidence, suggest a root cause analysis and corrective actions. Respond with valid JSON only, no markdown.

JSON schema:
{""suggestedRootCause"":string,""suggestedCorrectiveActions"":string,""reasoning"":string}";

        var parts = new List<string>
        {
            $"Event Title: {eventTitle}",
            $"Description: {description}",
        };
        if (!string.IsNullOrWhiteSpace(location)) parts.Add($"Location: {location}");
        if (!string.IsNullOrWhiteSpace(summary)) parts.Add($"Investigation Summary: {summary}");
        if (witnessStatements.Count > 0) parts.Add($"Witness Statements:\n{string.Join("\n---\n", witnessStatements)}");
        if (evidenceDescriptions.Count > 0) parts.Add($"Evidence:\n{string.Join("\n- ", evidenceDescriptions)}");

        var json = await CallClaudeAsync(system, string.Join("\n\n", parts), ct);
        if (json == null)
            return new AiInvestigationResult(null, null, "AI request failed.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new AiInvestigationResult(
                GetNullableString(root, "suggestedRootCause"),
                GetNullableString(root, "suggestedCorrectiveActions"),
                GetNullableString(root, "reasoning") ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI investigation response");
            return new AiInvestigationResult(null, null, "Failed to parse AI response.");
        }
    }

    public async Task<string> AnalyzeTrendsAsync(
        IReadOnlyList<AlertInfo> recentAlerts,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return "AI service not configured.";

        var system = @"You are a safety and incident management analyst. Given a list of recent insight alerts, provide a concise narrative summary of trends, patterns, and recommended actions. Write 2-4 paragraphs. Do not use markdown headers. Be specific and actionable.";

        var alertText = string.Join("\n\n", recentAlerts.Select(a =>
            $"[{a.Severity.ToUpperInvariant()}] {a.AlertType}: {a.Title}\n{a.Body}"));

        var prompt = $"Recent Insight Alerts ({recentAlerts.Count} total):\n\n{alertText}";

        var json = await CallClaudeAsync(system, prompt, ct);
        if (json == null) return "Unable to generate trend analysis.";

        // The response might be plain text or JSON-wrapped
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("summary", out var summaryEl))
                return summaryEl.GetString() ?? json;
        }
        catch
        {
            // Not JSON, return as plain text
        }

        return json;
    }

    private async Task<string?> CallClaudeAsync(string systemPrompt, string userMessage, CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient("ClaudeClient");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var payload = new
            {
                model = _model,
                max_tokens = 1024,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };

            var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Claude API error {Status}: {Body}", response.StatusCode, err);
                return null;
            }

            var result = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(result);

            // Extract text from content[0].text
            var content = doc.RootElement.GetProperty("content");
            if (content.GetArrayLength() > 0)
            {
                var text = content[0].GetProperty("text").GetString();
                return text;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude API call failed");
            return null;
        }
    }

    private static long? GetNullableLong(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt64();
        return null;
    }

    private static string? GetNullableString(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    private static double GetDouble(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetDouble();
        return 0;
    }
}
