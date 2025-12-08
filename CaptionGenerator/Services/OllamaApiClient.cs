using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CaptionGenerator.Services;

public class OllamaApiClient : IVisionLanguageModelClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaApiClient(string baseUrl, string model)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _model = model;
    }

    public async Task<string> GenerateCaptionAsync(byte[] imageData, string prompt)
    {
        var base64Image = Convert.ToBase64String(imageData);

        var requestData = new
        {
            model = _model,
            prompt = prompt,
            images = new[] { base64Image },
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/generate", requestData);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        return jsonResponse.GetProperty("response").GetString() ?? string.Empty;
    }
}
