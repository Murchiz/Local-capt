using System.Text.Json;
using System.Text.Json.Serialization;
using CaptionGenerator.Models;
using CaptionGenerator.ApiClients; // For the new OllamaRequest class below

namespace CaptionGenerator;

// 1. Register every class that gets converted to/from JSON here.
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(OllamaRequest))]
[JsonSerializable(typeof(JsonElement))] 
public partial class AppJsonContext : JsonSerializerContext
{
}