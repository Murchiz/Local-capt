using CaptionGenerator.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptionGenerator.ViewModels;

public partial class ImageCaptionViewModel : ObservableObject
{
    private readonly ImageCaption _imageCaption;

    [ObservableProperty]
    private bool _isProcessing;

    public ImageCaptionViewModel(ImageCaption imageCaption)
    {
        _imageCaption = imageCaption;
    }

    public string ImagePath
    {
        get => _imageCaption.ImagePath;
        set => SetProperty(_imageCaption.ImagePath, value, _imageCaption, (c, v) => c.ImagePath = v);
    }

    public string Caption
    {
        get => _imageCaption.Caption;
        set => SetProperty(_imageCaption.Caption, value, _imageCaption, (c, v) => c.Caption = v);
    }
}
