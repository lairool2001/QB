using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EverythingWithAI;

public class ClaudeService
{
    private readonly HttpClient _client;

    public ClaudeService(string apiKey)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> ConvertToEverythingQuery(string naturalLanguage)
    {
        var body = new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = 200,
            system = Strings.SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = naturalLanguage }
            }
        };

        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("https://api.anthropic.com/v1/messages", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            using var errDoc = JsonDocument.Parse(responseBody);
            var errMsg = errDoc.RootElement
                .TryGetProperty("error", out var err)
                ? err.GetProperty("message").GetString()
                : responseBody;
            throw new InvalidOperationException($"Claude API 錯誤：{errMsg}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return text?.Trim() ?? string.Empty;
    }
}
