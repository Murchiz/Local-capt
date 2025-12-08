using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CaptionGenerator.Services;

public class OpenAiCompatibleApiClient : IVisionLanguageModelClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OpenAiCompatibleApiClient(string baseUrl, string model)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _model = model;
    }

    public async Task<string> GenerateCaptionAsync(byte[] imageData, string prompt)
    {
        var base64Image = Convert.ToBase64String(imageData);
        var imageUrl = $"data:image/jpeg;base64,{base64Image}";

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

        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", requestData);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
