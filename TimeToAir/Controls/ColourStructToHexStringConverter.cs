using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Mooseware.TimeToAir.Controls
{
    public class ColourStructToHexStringConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Drawing.Color sdColour = (System.Drawing.Color)value;
            System.Windows.Media.Color swmColour = System.Windows.Media.Color.FromArgb(sdColour.A, sdColour.R, sdColour.G, sdColour.B);
            return new System.Windows.Media.SolidColorBrush(swmColour);
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            System.Windows.Media.SolidColorBrush brush = (System.Windows.Media.SolidColorBrush)value;
            System.Drawing.Color colour = System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
            return colour;
        }
    }
}
