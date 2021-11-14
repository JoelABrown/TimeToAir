using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Mooseware.TimeToAir.Themes.Styles
{
    internal static class AppResources
    {
        internal enum StaticResource
        {
            BrightForegroundBrush,
            SubduedForegroundBrush,
            CountdownNormalBackgroundBrush,
            CountdownNormalBackgroundBorderBrush,
            CountdownWarningBrush,
            CountdownWarningBackgroundBrush,
            CountdownWarningBackgroundBorderBrush,
            CuedBackgroundBrush,
            CuedBackgroundBorderBrush,
            CuedContrastBrush,
            CuedMainBrush,
            PlayingBackgroundBrush,
            PlayingBackgroundBorderBrush,
            PlayingMainBrush,
            PlayingContrastBrush,
            DisabledMainBrush,
            DisabledContrastBrush,
            TextBoxForegroundBrush,
            DarkBackgroundBrush,
            DisabledBackgroundBrush
        }

        internal static Brush DefinedColour(StaticResource colour)
        {
            // NOTE: This relies on the Enum name being the same as the StaticResource x:Key
            return Application.Current.Resources[colour.ToString()] as Brush;
        }
    }
}
