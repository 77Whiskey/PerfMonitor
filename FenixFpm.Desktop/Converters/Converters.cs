using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FenixFpm.Desktop.Converters;

public class BoolToOnOffConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "ON" : "OFF";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BooleanToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "True";
    public string FalseValue { get; set; } = "False";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? TrueValue : FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NavigationStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var currentView = value as string ?? "";
        var targetView = parameter as string ?? "";
        
        if (currentView.Equals(targetView, StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current.FindResource("ActiveNavigationButtonStyle");
        }
        return Application.Current.FindResource("NavigationButtonStyle");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
