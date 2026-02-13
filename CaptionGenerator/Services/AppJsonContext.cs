using System.Text.Json;
using System.Text.Json.Serialization;
using CaptionGenerator.Models;
using CaptionGenerator.ApiClients;

namespace CaptionGenerator;

// Register all types used in JSON serialization here
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(OllamaRequest))]
[JsonSerializable(typeof(OpenAiRequest))]
[JsonSerializable(typeof(OpenAiMessage))]
[JsonSerializable(typeof(OpenAiContent))]
[JsonSerializable(typeof(OpenAiImageUrl))]
[JsonSerializable(typeof(JsonElement))]
public partial class AppJsonContext : JsonSerializerContext
{
}
