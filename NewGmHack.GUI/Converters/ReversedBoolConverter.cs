using System.Globalization;
using System.Windows.Data;

namespace NewGmHack.GUI.Converters
{
    // i should partial that method , but now lazy with resharper
    internal class ReversedBoolConverter : IValueConverter
    {
        /// <inheritdoc />
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool oriValue)
            {
                return !oriValue;
            }

            return false;
        }

        /// <inheritdoc />
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}