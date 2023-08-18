using Mooseware.TimeToAir.Themes.Styles;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mooseware.TimeToAir
{
    /// <summary>
    /// Interaction logic for CountdownViewer.xaml
    /// </summary>
    public partial class CountdownViewer : Window
    {
        /// <summary>
        /// The window mode of the viewer
        /// </summary>
        public enum ViewerMode
        {
            /// <summary>
            /// Restored to normal size (movable, sizable, has chrome)
            /// </summary>
            Normal,
            /// <summary>
            /// Maximized to full screen with chrome hidden
            /// </summary>
            Fullscreen
        }

        /// <summary>
        /// The colour of the countdown timer text
        /// </summary>
        public enum CountdownColour
        {
            White,
            Green,
            Yellow,
            Red
        }

        /// <summary>
        /// Sentinel used to override the default behaviour of canceling attempts to close the viewer window
        /// </summary>
        private bool _reallyCloseThisTime = false;

        public CountdownViewer()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set the filespec of the background image to display in the countdown viewer
        /// </summary>
        /// <param name="wallpaperFilespec">Full path and file spec of an image</param>
        public void SetWallpaperImage(string wallpaperFilespec)
        {
            if (File.Exists(wallpaperFilespec))
            {
                WallpaperImage.Source = new BitmapImage(new Uri(wallpaperFilespec));
            }
            else
            {
                WallpaperImage.Source = null;
            }
        }

        /// <summary>
        /// Set whether the ON AIR indicator is shown
        /// </summary>
        /// <param name="showOnAir">True to show the ON AIR indicator, false to hide it</param>
        public void SetOnAir(bool showOnAir)
        {
            if (showOnAir)
            {
                OnAirBorder.Visibility = Visibility.Visible;
            }
            else
            {
                OnAirBorder.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Set the amount of time remaining until the service start time
        /// </summary>
        /// <param name="tMinus">Time remaining (negative value is post service start)</param>
        public void SetCountdownTime(TimeSpan tMinus)
        {
            if (tMinus.TotalSeconds >= 36000)
            {
                CountdownTime.Text = tMinus.ToString("hh\\:mm\\:ss");
            }
            else if (tMinus.TotalSeconds >= 3600)
            {
                CountdownTime.Text = tMinus.ToString("h\\:mm\\:ss");
            }
            else if (tMinus.TotalSeconds >= 600)
            {
                CountdownTime.Text = tMinus.ToString("mm\\:ss");
            }
            else 
            {
                CountdownTime.Text = tMinus.ToString("m\\:ss");
            }
        }

        /// <summary>
        /// Set the font size of the countdown time. Affects the other font sizes and padding as well proportionally.
        /// </summary>
        /// <param name="fontSize">New size of the countdown time in pixels</param>
        public void SetBaseFontSize(int fontSize)
        {
            CountdownTime.FontSize = fontSize;
            CountdownTime.Margin = new Thickness((fontSize * 0.375), 0, (fontSize * 0.375), (fontSize / 8.0));
            OnAirLabel.FontSize = fontSize * 0.75;  // ON AIR box should be roughly the same size as the countdown timer
            OnAirLabel.Margin = new Thickness((OnAirLabel.FontSize * 0.375), (OnAirLabel.FontSize / 6.0), (OnAirLabel.FontSize / 2.0), (OnAirLabel.FontSize / 3.0));

            SetCountdownMinWidth();
        }

        /// <summary>
        /// Set the margin from the side of the screen for the countdown time and on air indicator
        /// </summary>
        /// <param name="marginPercent">Expressed as an integer percent (e.g. 10=10%)</param>
        public void SetHorizontalMargin(int marginPercent)
        {
            double marginX = this.ActualWidth * (double)marginPercent / 100.0;
            Thickness countDownMargin = CountdownBorder.Margin;
            Thickness onAirMargin = OnAirBorder.Margin;

            countDownMargin.Right = marginX;
            onAirMargin.Left = marginX;

            CountdownBorder.Margin = countDownMargin;
            OnAirBorder.Margin = onAirMargin;
        }

        /// <summary>
        /// Set the margin from the bottom of the screen for the countdown time and on air indicator
        /// </summary>
        /// <param name="marginPercent">Expressed as an integer percent (e.g. 10=10%)</param>
        public void SetBottomMargin(int marginPercent)
        {
            double marginY = this.ActualHeight * (double)marginPercent / 100.0;
            Thickness countDownMargin = CountdownBorder.Margin;
            Thickness onAirMargin = OnAirBorder.Margin;

            countDownMargin.Bottom = marginY;
            onAirMargin.Bottom = marginY - OnAirBorder.BorderThickness.Bottom;

            CountdownBorder.Margin = countDownMargin;
            OnAirBorder.Margin = onAirMargin;
        }

        /// <summary>
        /// Change the window to either restored down/movable mode or fullscreen mode
        /// </summary>
        /// <param name="mode">The mode to assume</param>
        public void SetViewerMode(ViewerMode mode)
        {
            switch (mode)
            {
                case ViewerMode.Normal:
                    this.ResizeMode = ResizeMode.CanResize;
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                    this.WindowState = WindowState.Normal;
                    this.Topmost = false;

                    break;
                case ViewerMode.Fullscreen:
                    this.ResizeMode = ResizeMode.NoResize;
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Maximized;
                    this.Topmost = true;

                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Set the text colour of the countdown time for extra context
        /// </summary>
        /// <param name="countdownColour"></param>
        public void SetCountdownColour(CountdownColour countdownColour)
        {
            switch (countdownColour)
            {
                case CountdownColour.White:
                    CountdownTime.Foreground = AppResources.DefinedColour(AppResources.StaticResource.TextBoxForegroundBrush);
                    break;
                case CountdownColour.Green:
                    CountdownTime.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CuedMainBrush);
                    break;
                case CountdownColour.Yellow:
                    CountdownTime.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CountdownWarningBrush);
                    break;
                case CountdownColour.Red:
                    CountdownTime.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PlayingMainBrush);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Call to explicitly close the viewer window (manual attempts are rejected by default)
        /// </summary>
        public void ShutDownViewer()
        {
            _reallyCloseThisTime = true;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_reallyCloseThisTime)
            {
                // Don't allow the window to close on its own.
                // The parent window (MainWindow) must close it.
                e.Cancel = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetCountdownMinWidth();
        }

        /// <summary>
        /// Establish minimum width for the countdown proportional to the current font size so that it doesn't get too small under 60 seconds to air.
        /// </summary>
        private void SetCountdownMinWidth()
        {
            FormattedText formattedText = new("00:00",
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(CountdownTime.FontFamily, CountdownTime.FontStyle, CountdownTime.FontWeight, CountdownTime.FontStretch),
                CountdownTime.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Size block = new(formattedText.Width, formattedText.Height);

            CountdownTime.MinWidth = block.Width;
        }
    }
}
