using System;
using System.Globalization;
using System.Windows.Data;

namespace Animate
{
    public class DblToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
                return doubleValue.ToString();
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (double.TryParse(text, out double result))
                return result;

            // En cas d'erreur, retourne une valeur par défaut (ou DependencyProperty.UnsetValue)
            return 0;
        }
    }
}