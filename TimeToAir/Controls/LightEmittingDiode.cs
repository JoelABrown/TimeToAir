using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;

namespace Mooseware.TimeToAir.Controls
{
    public class LightEmittingDiode : Control
    {
        public enum ColourOptions
        {
            Green,
            Red,
            Yellow,
            White,
            Blue,
            Custom
        }

        // -- IsOn Property
        ////public static readonly DependencyProperty IsOnProperty =
        ////    DependencyProperty.Register("IsOn", typeof(bool), typeof(LightEmittingDiode),
        ////        new FrameworkPropertyMetadata(true,
        ////            new PropertyChangedCallback(OnIsOnPropertyChanged)));

        ////private static void OnIsOnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        ////{
        ////    if (d is LightEmittingDiode control)
        ////    {
        ////        control.OnIsOnPropertyChanged((bool)e.OldValue, (bool)e.NewValue);
        ////    }
        ////}

        ////protected virtual void OnIsOnPropertyChanged(bool oldValue, bool newValue)
        ////{
        ////    // Do the actual stuff that happens when the property changes.
        ////}

        // -- IsOn Property
        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register("IsOn", typeof(bool), typeof(LightEmittingDiode),
                new FrameworkPropertyMetadata(true,
                    FrameworkPropertyMetadataOptions.AffectsRender));
        public bool IsOn
        {
            get { return (bool)GetValue(IsOnProperty); }
            set { SetValue(IsOnProperty, value); }
        }

        // -- CurrentColour Property
        public static readonly DependencyPropertyKey CurrentColourPropertyKey
            = DependencyProperty.RegisterReadOnly(
                nameof(CurrentColour),
                typeof(Color),
                typeof(LightEmittingDiode),
                new FrameworkPropertyMetadata(Color.FromArgb(255, 0, 0),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurrentColourProperty
            = CurrentColourPropertyKey.DependencyProperty;

        public Color CurrentColour
        {
            get { return (Color)GetValue(CurrentColourProperty); }
            protected set { SetValue(CurrentColourPropertyKey, value); }   // S/B CurrentColourPropertyKey??? 
        }

        // -- SelectedColour property
        private ColourOptions _SelectedColour = ColourOptions.Green;
        public ColourOptions SelectedColour
        {
            get { return _SelectedColour; }
            set
            {
                _SelectedColour = value;
                // Calculate the appropriate current colour...
                switch (_SelectedColour)
                {
                    case ColourOptions.Green:
                        this.CurrentColour = Color.FromArgb(0, 255, 0);
                        break;
                    case ColourOptions.Red:
                        this.CurrentColour = Color.FromArgb(255, 0, 0);
                        break;
                    case ColourOptions.Yellow:
                        this.CurrentColour = Color.FromArgb(255, 255, 0);
                        break;
                    case ColourOptions.White:
                        this.CurrentColour = Color.FromArgb(255, 255, 255);
                        break;
                    case ColourOptions.Blue:
                        this.CurrentColour = Color.FromArgb(0, 127, 255);
                        break;
                    case ColourOptions.Custom:
                        this.CurrentColour = _CustomColour;
                        break;
                    default:
                        break;
                }
            }
        }

        // -- CustomColour property
        private Color _CustomColour = Color.FromArgb(255, 215, 0);
        public Color CustomColour
        {
            get { return _CustomColour; }
            set
            {
                _CustomColour = value;
                if (this.SelectedColour == ColourOptions.Custom)
                {
                    // Update the current colour...
                    this.CurrentColour = _CustomColour;
                }
            }
        }

        static LightEmittingDiode()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LightEmittingDiode),
                new FrameworkPropertyMetadata(typeof(LightEmittingDiode)));
        }
    }
}
