using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Services;
using CaptionGenerator;

namespace CaptionGenerator.ApiClients;

// Define the request/response structures explicitly so the Trimmer can see it.
public record OllamaRequest(string model, string prompt, byte[][] images, bool stream);
public record OllamaResponse(string response);

public class OllamaApiClient : IVisionLanguageModelClient
{
    private readonly string _model;
    private readonly Uri _generateUri;

    public OllamaApiClient(string baseUrl, string model)
    {
        _model = model;
        // ⚡ Bolt Optimization: Cache the Uri object to avoid repeated string formatting and parsing for every call.
        _generateUri = new Uri($"{baseUrl}/api/generate");
    }

    public async Task<string> GenerateCaptionAsync(byte[] imageData, string prompt)
    {
        var requestData = new OllamaRequest(
            _model,
            prompt,
            new[] { imageData },
            false
        );

        var request = new HttpRequestMessage(HttpMethod.Post, _generateUri)
        {
            Content = JsonContent.Create(requestData, AppJsonContext.Default.OllamaRequest)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // ⚡ Bolt Optimization: Use typed response deserialization to eliminate string-based property lookups and reduce memory overhead.
        var ollamaResponse = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.OllamaResponse);
        return ollamaResponse?.response ?? string.Empty;
    }
}
