using System.Text.Json;
using System.Text.Json.Serialization;
using CaptionGenerator.Models;
using CaptionGenerator.ApiClients;

namespace CaptionGenerator;

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(OllamaRequest))]
[JsonSerializable(typeof(OpenAiRequest))] // <--- ADD THIS
[JsonSerializable(typeof(JsonElement))]
public partial class AppJsonContext : JsonSerializerContext
{
}