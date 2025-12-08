using System.Collections.ObjectModel;

namespace CaptionGenerator.Models;

public class Settings
{
    public ObservableCollection<ApiEndpointSetting> ApiEndpoints { get; set; } = new();
    public ObservableCollection<PromptTemplateSetting> PromptTemplates { get; set; } = new();
    public bool EnableAsyncProcessing { get; set; } = false;
}
