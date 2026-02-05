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
        var base64Image = Convert.ToBase64String(imageData);
        var mimeType = GetMimeType(imageData);
        var imageUrl = $"data:{mimeType};base64,{base64Image}";

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

    private string GetMimeType(byte[] imageData)
    {
        if (imageData.Length > 4 && imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
        {
            return "image/png";
        }
        if (imageData.Length > 2 && imageData[0] == 0xFF && imageData[1] == 0xD8)
        {
            return "image/jpeg";
        }
        if (imageData.Length > 2 && imageData[0] == 0x42 && imageData[1] == 0x4D)
        {
            return "image/bmp";
        }
        return "image/jpeg"; // Default
    }
}
