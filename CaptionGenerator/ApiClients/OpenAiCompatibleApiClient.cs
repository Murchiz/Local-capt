using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Buffers.Binary;
using System.Text.Json;
using System.Threading.Tasks;
using CaptionGenerator.Services;
using CaptionGenerator;

namespace CaptionGenerator.ApiClients;

public record OpenAiRequest(string model, OpenAiMessage[] messages);
public record OpenAiMessage(string role, OpenAiContent[] content);
public record OpenAiContent(string type, string? text = null, OpenAiImageUrl? image_url = null);
public record OpenAiImageUrl(string url);

// Response records
public record OpenAiResponse(OpenAiChoice[] choices);
public record OpenAiChoice(OpenAiResponseMessage message);
public record OpenAiResponseMessage(string content);

public class OpenAiCompatibleApiClient : IVisionLanguageModelClient
{
    private readonly string _model;
    private readonly Uri _completionsUri;

    // ⚡ Bolt Optimization: Pre-calculate common Data URI prefixes to avoid string allocations for every image.
    private const string JpegPrefix = "data:image/jpeg;base64,";
    private const string PngPrefix = "data:image/png;base64,";
    private const string GifPrefix = "data:image/gif;base64,";
    private const string BmpPrefix = "data:image/bmp;base64,";

    public OpenAiCompatibleApiClient(string baseUrl, string model)
    {
        _model = model;
        // ⚡ Bolt Optimization: Cache the Uri object to avoid repeated string formatting and parsing for every call.
        _completionsUri = new Uri($"{baseUrl}/v1/chat/completions");
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

        var request = new HttpRequestMessage(HttpMethod.Post, _completionsUri)
        {
            Content = JsonContent.Create(requestData, AppJsonContext.Default.OpenAiRequest)
        };

        var response = await HttpClientContainer.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // ⚡ Bolt Optimization: Use typed response deserialization to eliminate string-based property lookups and reduce memory overhead.
        var openAiResponse = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.OpenAiResponse);

        if (openAiResponse?.choices is [var choice, ..] && choice.message != null)
        {
            return choice.message.content;
        }

        return string.Empty;
    }

    private static string GetMimeType(ReadOnlySpan<byte> data)
    {
        // ⚡ Bolt Optimization: Zero-allocation file signature detection using 32-bit signature checks.
        // This performs a single 32-bit comparison instead of multiple 8-bit checks, reducing CPU cycles.
        if (data.Length >= 4)
        {
            uint header = BinaryPrimitives.ReadUInt32BigEndian(data);
            if (header == 0x89504E47) return "image/png"; // PNG
            if (header == 0x47494638) return "image/gif"; // GIF8
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
        // ⚡ Bolt Optimization: Use pre-calculated prefixes for common image types.
        string prefix = mimeType switch
        {
            "image/jpeg" => JpegPrefix,
            "image/png" => PngPrefix,
            "image/gif" => GifPrefix,
            "image/bmp" => BmpPrefix,
            _ => $"data:{mimeType};base64,"
        };

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
