using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CaptionGenerator.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isProcessing && isProcessing)
        {
            return Brushes.Orange;
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
