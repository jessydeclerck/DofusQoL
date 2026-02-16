using System.Globalization;
using System.Windows.Data;

namespace DofusManager.UI.ViewModels;

public class BoolToStateConverter : IValueConverter
{
    public static readonly BoolToStateConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Minimisé" : "Actif";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
