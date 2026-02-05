using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Services;

namespace CaptionGenerator.ApiClients;

public class OllamaApiClient : IVisionLanguageModelClient
{
    private readonly string _baseUrl;
    private readonly string _model;

    // ⚡ Bolt Optimization: Pre-configure JsonSerializerOptions to avoid repeated discovery/allocation.
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = null // Ollama uses exact names in its API
    };

    public OllamaApiClient(string baseUrl, string model)
    {
        _baseUrl = baseUrl;
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

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
        {
            Content = JsonContent.Create(requestData, options: _jsonOptions)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return jsonResponse.GetProperty("response").GetString() ?? string.Empty;
    }
}
