using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace CaptionGenerator.Converters;

/// <summary>
/// ⚡ Bolt Optimization:
/// A converter that loads a bitmap from a file path and decodes it to a specific width.
/// This significantly reduces memory usage when displaying many large images in a list.
/// Instead of loading a 10MB+ image for a 200px preview, it only loads what is necessary.
/// Expected Impact: ~90-95% reduction in memory usage for image previews.
/// </summary>
public class BitmapAssetValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                if (!File.Exists(path)) return null;

                // Open the file as a stream and decode it to a width of 200 pixels.
                // This matches the UI display size and saves a massive amount of memory
                // compared to loading full-resolution images.
                using var stream = File.OpenRead(path);
                return Bitmap.DecodeToWidth(stream, 200);
            }
            catch (Exception)
            {
                // If the image cannot be loaded, return null to show nothing
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
