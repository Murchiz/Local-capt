using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Services;
using CaptionGenerator;

namespace CaptionGenerator.ApiClients;

public record OpenAiRequest(string model, OpenAiMessage[] messages);
public record OpenAiMessage(string role, OpenAiContent[] content);
public record OpenAiContent(string type, string? text = null, OpenAiImageUrl? image_url = null);
public record OpenAiImageUrl(string url);

public class OpenAiCompatibleApiClient : IVisionLanguageModelClient
{
    private readonly string _baseUrl;
    private readonly string _model;

    public OpenAiCompatibleApiClient(string baseUrl, string model)
    {
        _baseUrl = baseUrl;
        _model = model;
    }

    public async Task<string> GenerateCaptionAsync(byte[] imageData, string prompt)
    {
        // ⚡ Bolt Optimization: Use zero-allocation prefix detection for MIME types.
        string mimeType = GetMimeType(imageData);

        // ⚡ Bolt Optimization: Use string.Create with Convert.TryToBase64Chars to build data URI in a single allocation.
        string base64Image = ConstructDataUri(mimeType, imageData);

        var requestData = new OpenAiRequest(
            _model,
            [
                new OpenAiMessage("user", [
                    new OpenAiContent("text", prompt),
                    new OpenAiContent("image_url", null, new OpenAiImageUrl(base64Image))
                ])
            ]
        );

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
        {
            Content = JsonContent.Create(requestData, AppJsonContext.Default.OpenAiRequest)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.JsonElement);

        if (jsonResponse.ValueKind == JsonValueKind.Object &&
            jsonResponse.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string GetMimeType(ReadOnlySpan<byte> data)
    {
        // ⚡ Bolt Optimization: Zero-allocation file signature detection using manual byte checks.
        // This is faster and avoids issues with Span.StartsWith overloads in some environments.
        if (data.Length >= 4)
        {
            // PNG: 89 50 4E 47
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
            // GIF: 47 49 46 38
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38) return "image/gif";
        }

        if (data.Length >= 3)
        {
            // JPEG: FF D8 FF
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        }

        if (data.Length >= 2)
        {
            // BMP: 42 4D
            if (data[0] == 0x42 && data[1] == 0x4D) return "image/bmp";
        }

        return "image/jpeg"; // Default to JPEG
    }

    private static string ConstructDataUri(string mimeType, byte[] imageData)
    {
        // ⚡ Bolt Optimization: Construct data URI (data:{mimeType};base64,{base64}) in a single allocation.
        string prefix = $"data:{mimeType};base64,";
        int base64Length = (int)(((long)imageData.Length + 2) / 3 * 4);

        return string.Create(prefix.Length + base64Length, (prefix, imageData), (span, state) =>
        {
            state.prefix.AsSpan().CopyTo(span);
            if (!Convert.TryToBase64Chars(state.imageData, span[state.prefix.Length..], out _))
            {
                // Fallback (should not be reached)
                string b64 = Convert.ToBase64String(state.imageData);
                b64.AsSpan().CopyTo(span[state.prefix.Length..]);
            }
        });
    }
}
