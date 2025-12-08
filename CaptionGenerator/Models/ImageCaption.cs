using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptionGenerator.Models;

public partial class ImageCaption : ObservableObject
{
    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private string _caption = string.Empty;
}
