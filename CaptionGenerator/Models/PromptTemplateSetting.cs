using System.Collections.Generic;

namespace CaptionGenerator.Models;

public class PromptTemplateSetting
{
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = "Text"; // "Text" or "Markdown"
    public List<ApiEndpointSetting> ApiEndpoints { get; set; } = new();
}
