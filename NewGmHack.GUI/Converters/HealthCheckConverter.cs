using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace NewGmHack.GUI.Converters
{
    public class HealthCheckConverter: IValueConverter
    {
        /// <inheritdoc />
        public object? Convert(object?     value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool connectionStatus)
            {
                return connectionStatus ? Brushes.Green : Brushes.Red;
            }

            return Brushes.Red;
        }

        /// <inheritdoc />
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
