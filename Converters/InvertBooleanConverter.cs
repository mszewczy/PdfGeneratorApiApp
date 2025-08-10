using System;
using System.Globalization;
using System.Windows.Data;

namespace PdfGeneratorApiApp.Converters
{
    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // POPRAWKA: Implementacja ConvertBack dla pełnej funkcjonalności konwertera
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return false;
        }
    }
}
