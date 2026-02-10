using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Services;

namespace CaptionGenerator.ApiClients;

// Define the request structure explicitly so the Trimmer can see it.
public record OllamaRequest(string model, string prompt, byte[][] images, bool stream);

public class OllamaApiClient : IVisionLanguageModelClient
{
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaApiClient(string baseUrl, string model)
    {
        _baseUrl = baseUrl;
        _model = model;
    }

    public async Task<string> GenerateCaptionAsync(byte[] imageData, string prompt)
    {
        // Use the concrete class instead of an anonymous object
        var requestData = new OllamaRequest(
            _model,
            prompt,
            new[] { imageData },
            false
        );

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
        {
            // FIX: Pass the specific TypeInfo from our Context
            Content = JsonContent.Create(requestData, AppJsonContext.Default.OllamaRequest)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // FIX: Use the Context for deserialization
        var jsonResponse = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.JsonElement);
        
        // Handle case where response might be null/empty safely
        if (jsonResponse.ValueKind == JsonValueKind.Object && 
            jsonResponse.TryGetProperty("response", out var responseText))
        {
            return responseText.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}