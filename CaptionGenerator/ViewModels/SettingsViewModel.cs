using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CaptionGenerator.Models;
using CaptionGenerator.Services;

namespace CaptionGenerator.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private Settings _settings = new();

    [ObservableProperty]
    private ObservableCollection<ApiEndpointSetting> _apiEndpoints = new();

    [ObservableProperty]
    private ObservableCollection<PromptTemplateSetting> _promptTemplates = new();

    [ObservableProperty]
    private ApiEndpointSetting? _selectedApiEndpoint;

    [ObservableProperty]
    private PromptTemplateSetting? _selectedPromptTemplate;

    [ObservableProperty]
    private bool _enableAsyncProcessing;

    public static List<string> Providers => new() { "Ollama", "LM Studio", "llama.cpp", "Oobabooga" };
    public static List<string> OutputFormats => new() { "Text", "Markdown" };

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.LoadSettingsAsync();
        if (!_settings.PromptTemplates.Any())
        {
            _settings.PromptTemplates.Add(new PromptTemplateSetting
            {
                Name = "Default",
                Prompt = "Generate a caption for this image. The output format should be {output_format}.",
                ModelName = "Default",
                OutputFormat = "Text"
            });
        }
        ApiEndpoints = _settings.ApiEndpoints;
        PromptTemplates = _settings.PromptTemplates;
        foreach (var template in PromptTemplates)
        {
            template.ApiEndpoints = ApiEndpoints.ToList();
        }
        EnableAsyncProcessing = _settings.EnableAsyncProcessing;
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        _settings.ApiEndpoints = ApiEndpoints;
        _settings.PromptTemplates = PromptTemplates;
        _settings.EnableAsyncProcessing = EnableAsyncProcessing;
        await _settingsService.SaveSettingsAsync(_settings);
    }

    [RelayCommand]
    private void AddApiEndpoint()
    {
        ApiEndpoints.Add(new ApiEndpointSetting { Name = "New Endpoint" });
    }

    [RelayCommand]
    private void RemoveApiEndpoint()
    {
        if (SelectedApiEndpoint != null)
        {
            ApiEndpoints.Remove(SelectedApiEndpoint);
        }
    }

    [RelayCommand]
    private void AddPromptTemplate()
    {
        PromptTemplates.Add(new PromptTemplateSetting
        {
            Name = "New Template",
            ApiEndpoints = ApiEndpoints.ToList()
        });
    }

    [RelayCommand]
    private void RemovePromptTemplate()
    {
        if (SelectedPromptTemplate != null)
        {
            PromptTemplates.Remove(SelectedPromptTemplate);
        }
    }
}
