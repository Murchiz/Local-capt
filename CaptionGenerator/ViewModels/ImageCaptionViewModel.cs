using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using CaptionGenerator.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptionGenerator.ViewModels;

public partial class ImageCaptionViewModel : ObservableObject
{
    private readonly ImageCaption _imageCaption;
    private string _persistedCaption;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isModified;

    public ImageCaptionViewModel(ImageCaption imageCaption)
    {
        _imageCaption = imageCaption;
        _persistedCaption = imageCaption.Caption;
    }

    public string ImagePath
    {
        get => _imageCaption.ImagePath;
        set => SetProperty(_imageCaption.ImagePath, value, _imageCaption, (c, v) => c.ImagePath = v);
    }

    public string Caption
    {
        get => _imageCaption.Caption;
        set
        {
            if (SetProperty(_imageCaption.Caption, value, _imageCaption, (c, v) => c.Caption = v))
            {
                IsModified = _imageCaption.Caption != _persistedCaption;
            }
        }
    }

    public string Extension
    {
        get => _imageCaption.Extension;
        set => SetProperty(_imageCaption.Extension, value, _imageCaption, (c, v) => c.Extension = v);
    }

    /// <summary>
    /// Updates the caption from an external source (e.g. AI generation or initial load)
    /// without marking it as modified by the user.
    /// </summary>
    public void UpdateCaptionProgrammatically(string newCaption)
    {
        _imageCaption.Caption = newCaption;
        _persistedCaption = newCaption;
        OnPropertyChanged(nameof(Caption));
        IsModified = false;
    }

    /// <summary>
    /// Marks the current caption as persisted, clearing the modified state.
    /// Useful after a bulk save operation.
    /// </summary>
    public void MarkAsPersisted()
    {
        _persistedCaption = Caption;
        IsModified = false;
    }

    [RelayCommand]
    private async Task SaveCaptionAsync()
    {
        var captionPath = Path.ChangeExtension(ImagePath, ".txt");

        // ⚡ Bolt Optimization: Use ArrayPool to avoid byte[] allocations for individual caption saving.
        var captionSpan = Caption.AsSpan();
        int maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(captionSpan.Length);
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            int written = System.Text.Encoding.UTF8.GetBytes(captionSpan, rentedBuffer);
            await File.WriteAllBytesAsync(captionPath, rentedBuffer.AsMemory(0, written));

            MarkAsPersisted();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
