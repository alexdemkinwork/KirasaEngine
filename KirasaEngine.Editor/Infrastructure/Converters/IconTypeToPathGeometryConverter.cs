using Avalonia.Data.Converters;

namespace KirasaEngine.Editor.Infrastructure.Converters;

public class IconTypeToPathGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Application.Current!.FindResource(Enum.GetName((IconType)value!)!);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
