using System;
using System.IO;
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

    // ⚡ Bolt Optimization: Pre-configure JsonSerializerOptions to avoid repeated discovery/allocation.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public OpenAiCompatibleApiClient(string baseUrl, string model)
    {
        _baseUrl = baseUrl;
        _model = model;
    }

    public async Task<string> GenerateCaptionAsync(byte[] imageData, string prompt)
    {
        var mimeType = GetMimeType(imageData);
        var imageUrl = CreateDataUri(imageData, mimeType);

        var requestData = new
        {
            model = _model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = imageUrl } }
                    }
                }
            },
            max_tokens = 1024,
            stream = false
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
        {
            Content = JsonContent.Create(requestData, options: _jsonOptions)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    /// <summary>
    /// ⚡ Bolt Optimization: Build the data URI in a single allocation using string.Create.
    /// This avoids the intermediate large base64 string allocation from Convert.ToBase64String.
    /// </summary>
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
        // ⚡ Bolt Optimization: Use Span-based StartsWith for zero-allocation header checking.
        ReadOnlySpan<byte> pngHeader = [0x89, 0x50, 0x4E, 0x47];
        ReadOnlySpan<byte> jpegHeader = [0xFF, 0xD8];
        ReadOnlySpan<byte> bmpHeader = [0x42, 0x4D];

        if (imageData.StartsWith(pngHeader))
        {
            return "image/png";
        }
        if (imageData.StartsWith(jpegHeader))
        {
            return "image/jpeg";
        }
        if (imageData.StartsWith(bmpHeader))
        {
            return "image/bmp";
        }
        return "image/jpeg"; // Default
    }
}
