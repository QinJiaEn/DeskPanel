using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeskPanel.Services;

public static class AiCategorizationService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Send file names to OpenAI to get category assignments.
    /// Returns a dictionary mapping file name → category name.
    /// </summary>
    public static async Task<Dictionary<string, string>> CategorizeAsync(
        List<string> fileNames,
        List<string> categoryNames,
        string apiKey)
    {
        var settings = SettingsService.Current;
        var baseUrl = settings.AiBaseUrl;
        var model = settings.AiModel;

        Console.WriteLine("[AI] ========== 开始 AI 分类 ==========");
        Console.WriteLine($"[AI] 文件数量: {fileNames.Count}, 分类数量: {categoryNames.Count}");
        Console.WriteLine($"[AI] BaseURL: {baseUrl}");
        Console.WriteLine($"[AI] Model: {model}");
        Console.WriteLine($"[AI] ApiKey 前8位: {(apiKey.Length >= 8 ? apiKey[..8] : apiKey)}...");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI API Key 未配置，请在设置中填写。");
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("AI Base URL 未配置，请在设置中填写。");
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("AI Model 未配置，请在设置中填写。");
        if (fileNames.Count == 0)
            return new Dictionary<string, string>();
        if (categoryNames.Count == 0)
            throw new InvalidOperationException("没有可用的分类。");

        var cats = string.Join("、", categoryNames);
        var files = string.Join("\n", fileNames.Select((f, i) => $"{i + 1}. {f}"));

        var prompt = $@"你是一个文件整理助手。请根据文件名判断每个文件最合适的分类。

可选分类：{cats}

文件列表：
{files}

请返回 JSON 格式的映射，key 是文件序号（字符串），value 是分类名（必须是可选分类中的一个）：
{{""1"": ""分类名"", ""2"": ""分类名"", ...}}

只返回 JSON，不要其他内容。";

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "你是一个文件整理助手，只返回 JSON。" },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 500
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = baseUrl.TrimEnd('/') + "/chat/completions";
        Console.WriteLine($"[AI] 请求 URL: {url}");

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = content;

        var startTime = DateTime.Now;
        Console.WriteLine("[AI] 发送 HTTP 请求...");
        var response = await _http.SendAsync(request);
        var elapsed = (DateTime.Now - startTime).TotalSeconds;
        Console.WriteLine($"[AI] HTTP 响应状态: {response.StatusCode}, 耗时: {elapsed:F1}s");

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[AI] 原始响应体 ({responseBody.Length} 字符):");
        Console.WriteLine(responseBody.Length > 500 ? responseBody[..500] + "..." : responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        var msgObj = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");
        // Try content first; fallback to reasoning_content for reasoning models
        var message = msgObj.TryGetProperty("content", out var c) && c.GetString() is { Length: > 0 } s
            ? s
            : msgObj.TryGetProperty("reasoning_content", out var rc)
                ? rc.GetString()!
                : string.Empty;

        Console.WriteLine($"[AI] AI 返回内容: {message.Trim()}");

        // Parse the JSON response from AI
        var result = new Dictionary<string, string>();
        using var aiDoc = JsonDocument.Parse(message.Trim());
        var aiRoot = aiDoc.RootElement;

        foreach (var prop in aiRoot.EnumerateObject())
        {
            var index = int.Parse(prop.Name) - 1;
            if (index >= 0 && index < fileNames.Count)
            {
                var aiCategory = prop.Value.GetString()!;
                // Find the closest matching category name
                var matched = categoryNames.FirstOrDefault(c =>
                    c.Contains(aiCategory, StringComparison.OrdinalIgnoreCase) ||
                    aiCategory.Contains(c, StringComparison.OrdinalIgnoreCase))
                    ?? categoryNames.First();
                result[fileNames[index]] = matched;
                Console.WriteLine($"[AI]   {fileNames[index]} -> {matched}");
            }
        }

        Console.WriteLine($"[AI] ========== 分类完成: {result.Count} 个文件分类 ==========");
        return result;
    }
}
