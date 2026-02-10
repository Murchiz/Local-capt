using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Services;

namespace CaptionGenerator.ApiClients;

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
        var mimeType = GetMimeType(imageData);
        var imageUrl = CreateDataUri(imageData, mimeType);

        // 1. Create the request object using the explicit records defined below
        var requestData = new OpenAiRequest(
            _model,
            new[]
            {
                new OpenAiMessage(
                    "user",
                    new[]
                    {
                        new OpenAiContentPart("text", Text: prompt),
                        new OpenAiContentPart("image_url", ImageUrl: new OpenAiImageUrl(imageUrl))
                    }
                )
            },
            1024,
            false
        );

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
        {
            // 2. FIX: Use AppJsonContext for serialization
            Content = JsonContent.Create(requestData, AppJsonContext.Default.OpenAiRequest)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // 3. FIX: Use AppJsonContext for deserialization
        var jsonResponse = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.JsonElement);
        
        // Safely extract the content string
        if (jsonResponse.ValueKind == JsonValueKind.Object &&
            jsonResponse.TryGetProperty("choices", out var choices) &&
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

    private static string CreateDataUri(byte[] imageData, string mimeType)
    {
        int base64Length = (imageData.Length + 2) / 3 * 4;
        const string prefix = "data:";
        const string base64Marker = ";base64,";

        int totalLength = prefix.Length + mimeType.Length + base64Marker.Length + base64Length;

        return string.Create(totalLength, (imageData, mimeType), (span, state) =>
        {
            "data:".AsSpan().CopyTo(span);
            span = span.Slice(5);

            state.mimeType.AsSpan().CopyTo(span);
            span = span.Slice(state.mimeType.Length);

            ";base64,".AsSpan().CopyTo(span);
            span = span.Slice(8);

            if (!Convert.TryToBase64Chars(state.imageData, span, out _))
            {
                throw new InvalidOperationException("Failed to encode base64 data");
            }
        });
    }

    private static string GetMimeType(ReadOnlySpan<byte> imageData)
    {
        ReadOnlySpan<byte> pngHeader = [0x89, 0x50, 0x4E, 0x47];
        ReadOnlySpan<byte> jpegHeader = [0xFF, 0xD8];
        ReadOnlySpan<byte> bmpHeader = [0x42, 0x4D];

        if (imageData.StartsWith(pngHeader)) return "image/png";
        if (imageData.StartsWith(jpegHeader)) return "image/jpeg";
        if (imageData.StartsWith(bmpHeader)) return "image/bmp";
        return "image/jpeg"; 
    }
}

// --- Explicit Records for AOT/Trimming Compatibility ---

public record OpenAiRequest(
    string model,
    OpenAiMessage[] messages,
    int max_tokens,
    bool stream
);

public record OpenAiMessage(
    string role,
    OpenAiContentPart[] content
);

public record OpenAiContentPart(
    string type,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Text = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("image_url")]
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    OpenAiImageUrl? ImageUrl = null
);

public record OpenAiImageUrl(string url);