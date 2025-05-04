using BMDSwitcherAPI;
using Mooseware.TimeToAir.Controls;
using Mooseware.TimeToAir.Themes.Styles;
using SwitcherPanelCSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mooseware.TimeToAir
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Timer for keeping track of the current time and updating the countdown display
        /// </summary>
        private readonly DispatcherTimer _heartbeat;

        /// <summary>
        /// ATEM switcher discovery API
        /// </summary>
        private readonly IBMDSwitcherDiscovery _switcherDiscovery;

        /// <summary>
        /// ATEM Switcher API
        /// </summary>
        private IBMDSwitcher _atem;

        /// <summary>
        /// ATEM API for the first MixEffectBlock
        /// </summary>
        private IBMDSwitcherMixEffectBlock _me0;

        /// <summary>
        /// Utility class for hooking callbacks from the MixEffectBlock
        /// </summary>
        private readonly MixEffectBlockMonitor _mixEffectBlockMonitor;

        /// <summary>
        /// Reference to the 1st ME block's Auxiliary Output used to set the output source
        /// </summary>
        private IBMDSwitcherInputAux _auxOut;

        /// <summary>
        /// Input ID of the configured clip input (where the countdown viewer is displayed)
        /// </summary>
        private long _clipInputId;

        /// <summary>
        /// Input ID of the first Media Player (if MP1 is in program, we are NOT on air)
        /// </summary>
        private long _inputMp1Id;

        /// <summary>
        /// Input ID of the second Media Player (if MP2 is in program, we are NOT on air)
        /// </summary>
        private long _inputMp2Id;

        /// <summary>
        /// Input ID of the PROGRAM OUT input. Used to set the Aux Out to Program (normal operation)
        /// </summary>
        private long _pgmOutId;

        /// <summary>
        /// Input ID of the PREVIEW OUT input. Used to confirm that this input is active in the ATEM
        /// </summary>
        private long _pvwOutId;

        /// <summary>
        /// Date and time of the start of the next service. This is the point in time we are counting down to.
        /// </summary>
        private DateTime _OnAirTime = DateTime.MinValue;

        /// <summary>
        /// Sentinel to prevent null references to controls while loading the form.
        /// </summary>
        private bool _loaded = false;   // Sentinel to prevent null refs while loading the window.

        /// <summary>
        /// Window where the countdown time and on air notice are shown
        /// </summary>
        private readonly CountdownViewer _countdownViewer = new();

        /// <summary>
        /// Flag to not whether the ATEM connection test has succeeded
        /// </summary>
        private bool IsAtemConnected { get; set; }

        /// <summary>
        /// Action to take when the Run button is pressed or the countdown time elapses
        /// </summary>
        private enum RunAction 
        { 
            /// <summary>
            /// Stop if running, run if stopped
            /// </summary>
            Toggle, 
            /// <summary>
            /// Run if not already running (otherwise no effect)
            /// </summary>
            ForceRun, 
            /// <summary>
            /// Stop if running (otherwise no effect)
            /// </summary>
            ForceStop 
        };

        public MainWindow()
        {
            InitializeComponent();

            _heartbeat = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _heartbeat.Tick += Heartbeat_Tick;

            // Wire up ATEM switch event of interest.
            // note: this invoke pattern ensures our callback is called in the main thread. We are making double
            // use of lambda expressions here to achieve this.
            // Essentially, the events will arrive at the callback class (implemented by our monitor classes)
            // on a separate thread. We must marshal these to the main thread, and we're doing this by calling
            // invoke on the Windows Forms object. The lambda expression is just a simplification.
            _mixEffectBlockMonitor = new MixEffectBlockMonitor();
            _mixEffectBlockMonitor.ProgramInputChanged += new SwitcherEventHandler((s, a) => this.Dispatcher.Invoke((Action)(() => AtemProgramInputChanged())));

            _switcherDiscovery = new CBMDSwitcherDiscovery();

            IsAtemConnected = false;

            _countdownViewer.Show();
        }

        /// <summary>
        /// Event fired if a change to the ATEM's program input source is detected. This determines whether the ON AIR display is needed.
        /// </summary>
        private void AtemProgramInputChanged()
        {
            SetOnAirDisplay();
        }

        /// <summary>
        /// Load the settings from local storage and apply defaults, if necessary.
        /// </summary>
        private static void LoadSettings()
        {
            bool dirty = false;

            LocalConfiguration.Load();
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.AtemIpAddress))
            {
                LocalConfiguration.Settings.AtemIpAddress = Properties.Settings.Default.AtemIpAddress;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.InputCountdown))
            {
                LocalConfiguration.Settings.InputCountdown = Properties.Settings.Default.InputCountdownName;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.InputMP1))
            {
                LocalConfiguration.Settings.InputMP1 = Properties.Settings.Default.InputMP1Name;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.InputMP2))
            {
                LocalConfiguration.Settings.InputMP2 = Properties.Settings.Default.InputMP2Name;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.EveningStart))
            {
                LocalConfiguration.Settings.EveningStart = Properties.Settings.Default.EveningStart;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.MorningStart))
            {
                LocalConfiguration.Settings.MorningStart = Properties.Settings.Default.MorningStart;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.LastCustomStart))
            {
                LocalConfiguration.Settings.LastCustomStart = Properties.Settings.Default.LastCustomStart;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.WallpaperFilespec))
            {
                LocalConfiguration.Settings.WallpaperFilespec = Properties.Settings.Default.WallpaperFilespec;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.TurnGreen))
            {
                LocalConfiguration.Settings.TurnGreen = Properties.Settings.Default.TurnGreen;
                dirty = true;
            }
            if (string.IsNullOrEmpty(LocalConfiguration.Settings.TurnYellow))
            {
                LocalConfiguration.Settings.TurnYellow = Properties.Settings.Default.TurnYellow;
                dirty = true;
            }
            if (LocalConfiguration.Settings.XPosition == 0)
            {
                LocalConfiguration.Settings.XPosition = Properties.Settings.Default.XPosition;
                dirty = true;
            }
            if (LocalConfiguration.Settings.YPosition == 0)
            {
                LocalConfiguration.Settings.YPosition = Properties.Settings.Default.YPosition;
                dirty = true;
            }
            if (LocalConfiguration.Settings.FontSize == 0)
            {
                LocalConfiguration.Settings.FontSize = Properties.Settings.Default.FontSize;
                dirty = true;
            }
            if (LocalConfiguration.Settings.WindowLocation.X == 0
                && LocalConfiguration.Settings.WindowLocation.Y == 0)
            {
                LocalConfiguration.Settings.WindowLocation = new System.Drawing.Point(
                    Properties.Settings.Default.WindowLocation.X,
                    Properties.Settings.Default.WindowLocation.Y);
                dirty = true;
            }
            if (LocalConfiguration.Settings.WindowSize.Width == 0
                && LocalConfiguration.Settings.WindowSize.Height == 0)
            {
                LocalConfiguration.Settings.WindowSize = new System.Drawing.Size(
                    Properties.Settings.Default.WindowSize.Width,
                    Properties.Settings.Default.WindowSize.Height);
                dirty = true;
            }
            // No need to initialize RestoreWindowLocation or FullScreenViewer or SuppressOnAirNotificationCheckbox
            if (dirty)
            {
                LocalConfiguration.Save();
            }
        }

        private void ApplyOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            bool atemReconnectRequired = false; // or any other API object needs to be re-initialized.
            bool anyOtherSettingChanged = false;
            string formattedTimeOfDay;
            string formattedTimeSpan;

            if (AtemIpAddressTextBox.Text.Trim() != LocalConfiguration.Settings.AtemIpAddress)
            {
                LocalConfiguration.Settings.AtemIpAddress = AtemIpAddressTextBox.Text.Trim();
                atemReconnectRequired = true;
            }
            if (CountdownInputTextBox.Text.Trim() != LocalConfiguration.Settings.InputCountdown)
            {
                LocalConfiguration.Settings.InputCountdown = CountdownInputTextBox.Text.Trim();
                atemReconnectRequired = true;
            }
            if (MP1InputTextBox.Text.Trim() != LocalConfiguration.Settings.InputMP1)
            {
                LocalConfiguration.Settings.InputMP1 = MP1InputTextBox.Text.Trim();
                atemReconnectRequired = true;
            }
            if (MP2InputTextBox.Text.Trim() != LocalConfiguration.Settings.InputMP2)
            {
                LocalConfiguration.Settings.InputMP2 = MP2InputTextBox.Text.Trim();
                atemReconnectRequired = true;
            }
            if (EveningServiceStartTimeTextBox.Text.Trim() != LocalConfiguration.Settings.EveningStart)
            {
                // Is the new value valid?
                if (IsValidTimeOfDayString(EveningServiceStartTimeTextBox.Text, out formattedTimeOfDay))
                {
                    LocalConfiguration.Settings.EveningStart = formattedTimeOfDay;
                    anyOtherSettingChanged = true;
                }
            }
            if (MorningServiceStartTimeTextBox.Text.Trim() != LocalConfiguration.Settings.MorningStart)
            {
                // Is the new value valid?
                if (IsValidTimeOfDayString(MorningServiceStartTimeTextBox.Text, out formattedTimeOfDay))
                {
                    LocalConfiguration.Settings.MorningStart = formattedTimeOfDay;
                    anyOtherSettingChanged = true;
                }
            }
            if (CustomStartTimeTextBox.Text.Trim() != LocalConfiguration.Settings.LastCustomStart)
            {
                // Is the new value valid?
                if (IsValidTimeOfDayString(CustomStartTimeTextBox.Text, out formattedTimeOfDay))
                {
                    LocalConfiguration.Settings.LastCustomStart = formattedTimeOfDay;
                    anyOtherSettingChanged = true;
                }
            }
            if (WallpaperFilespecTextBox.Text.Trim() != LocalConfiguration.Settings.WallpaperFilespec)
            {
                LocalConfiguration.Settings.WallpaperFilespec = WallpaperFilespecTextBox.Text.Trim();
                atemReconnectRequired = true;
            }
            if (int.TryParse(XPositionTextBox.Text.Trim(), out int newXPosition))
            {
                if (XPositionTextBox.Text.Trim() != LocalConfiguration.Settings.XPosition.ToString())
                {
                    LocalConfiguration.Settings.XPosition = newXPosition;
                    atemReconnectRequired = true;
                }
            }
            if (int.TryParse(YPositionTextBox.Text.Trim(), out int newYPosition))
            {
                if (YPositionTextBox.Text.Trim() != LocalConfiguration.Settings.YPosition.ToString())
                {
                    LocalConfiguration.Settings.YPosition = newYPosition;
                    atemReconnectRequired = true;
                }
            }
            if (TurnGreenTextBox.Text.Trim() != LocalConfiguration.Settings.TurnGreen)
            {
                // Is the new value valid?
                if (IsValidMinutesAndSeconds(TurnGreenTextBox.Text, out formattedTimeSpan))
                {
                    LocalConfiguration.Settings.TurnGreen = formattedTimeSpan;
                    anyOtherSettingChanged = true;
                }
            }
            if (TurnYellowTextBox.Text.Trim() != LocalConfiguration.Settings.TurnYellow)
            {
                // Is the new value valid?
                if (IsValidMinutesAndSeconds(TurnYellowTextBox.Text, out formattedTimeSpan))
                {
                    LocalConfiguration.Settings.TurnYellow = formattedTimeSpan;
                    anyOtherSettingChanged = true;
                }
            }
            if (int.TryParse(FontSizeTextBox.Text.Trim(), out int newFontSize))
            {
                if (FontSizeTextBox.Text.Trim() != LocalConfiguration.Settings.FontSize.ToString())
                {
                    LocalConfiguration.Settings.FontSize = newFontSize;
                    anyOtherSettingChanged = true;
                }
            }

            if (atemReconnectRequired || anyOtherSettingChanged)
            {
                // Save settings changes...
                LocalConfiguration.Save();

                // Reconnect to ATEM API if required...
                if (atemReconnectRequired)
                {
                    ConnectToAtem();
                }

                // Refresh the display to pick up any formatting changes.
                ShowOptions();
            }
        }

        /// <summary>
        /// Tests a time of day string in HH:MM 24 hour format to see if it is a valid time of day
        /// </summary>
        /// <param name="raw">Input time of day string</param>
        /// <param name="formattedString">Valid time of day string in HH:MM 24 hour format, if available</param>
        /// <returns>True if the input string is a valid time of day</returns>
        private static bool IsValidTimeOfDayString(string raw, out string formattedString)
        {
            CultureInfo enCA = new("en-CA");
            formattedString = raw;

            bool result = DateTime.TryParseExact(raw, "H:mm", enCA, DateTimeStyles.None, out DateTime timeOfDay);
            if (result)
            {
                formattedString = timeOfDay.ToString("HH:mm");
            }
            return result;
        }

        /// <summary>
        /// Tests a durationg string expressed as minutes and seconds (MM:SS) to see if it is valid
        /// </summary>
        /// <param name="raw">Input duration in minutes and seconds (MM:SS)</param>
        /// <param name="formattedString">Valid duration string, if available</param>
        /// <returns>True if the input string is a valid duration in minutes and seconds</returns>
        private static bool IsValidMinutesAndSeconds(string raw, out string formattedString)
        {
            CultureInfo enCA = new("en-CA");
            formattedString = raw;

            bool result = TimeSpan.TryParseExact(raw, "%m\\:ss", enCA, out TimeSpan span);
            if (result)
            {
                formattedString = span.ToString("mm\\:ss");
            }
            return result;
        }

        /// <summary>
        /// Converts a valid duration expressed as a string in MM:SS format into the total number of seconds represented
        /// </summary>
        /// <param name="raw">Duration string in MM:SS format</param>
        /// <returns>The total seconds in the duration</returns>
        private static int SecondsFromMinutesAndSecondsString(string raw)
        {
            CultureInfo enCA = new("en-CA");
            int result = 0;
            if (IsValidMinutesAndSeconds(raw, out string formattedMinutesAndSeconds))
            {
                if (TimeSpan.TryParseExact(formattedMinutesAndSeconds,"%m\\:ss", enCA, out TimeSpan span))
                {
                    result = (int)span.TotalSeconds;
                }
            }
            return result;
        }

        /// <summary>
        /// Connect to the ATEM API based on current settings and test the availability of required inputs
        /// </summary>
        private void ConnectToAtem()
        {
            // Flags for status of various LED indicators.
            bool countDownInputOK = false;
            bool mp1InputOK = false;
            bool mp2InputOK = false;
            bool auxOutputOK = false;
            bool pvwOutputOK = false;
            bool pgmOutputOK = false;

            IsAtemConnected = false;
            try
            {
                _switcherDiscovery.ConnectTo(LocalConfiguration.Settings.AtemIpAddress, out _atem, out _BMDSwitcherConnectToFailure failReason);
                IsAtemConnected = true;

                // If this is a reconnection, get rid of the old references first
                if (_me0 != null)
                {
                    // Remove callback
                    _me0.RemoveCallback(_mixEffectBlockMonitor);

                    // Release reference
                    _me0 = null;
                }

                // We want to get the first Mix Effect block (ME 1). We create a ME iterator,
                // and then get the first one:
                _me0 = null;

                IBMDSwitcherMixEffectBlockIterator meIterator = null;
                Guid meIteratorIID = typeof(IBMDSwitcherMixEffectBlockIterator).GUID;
                _atem.CreateIterator(ref meIteratorIID, out IntPtr meIteratorPtr);
                meIterator = (IBMDSwitcherMixEffectBlockIterator)Marshal.GetObjectForIUnknown(meIteratorPtr);
                if (meIterator != null)
                {
                    meIterator.Next(out _me0);
                    if (_me0 == null)
                    {
                        MessageBox.Show("Unexpected: Could not get first mix effect block", "Error");
                        return;
                    }
                }
                else
                {
                    // Not good.
                    MessageBox.Show("Unexpected: Could not get mix effect block iterator", "Error");
                    return;
                }
                // Install MixEffectBlockMonitor callbacks:
                _me0.AddCallback(_mixEffectBlockMonitor);

                // Go through the inputs and find the ones of interest...
                foreach (var input in SwitcherInputs.ToList<IBMDSwitcherInput>())
                {
                    input.GetShortName(out string shortName);
                    input.GetInputId(out long inputId);
                    input.GetPortType(out _BMDSwitcherPortType portType);

                    // Is this the countdown input?
                    if (string.Compare(shortName.ToUpperInvariant(), LocalConfiguration.Settings.InputCountdown.ToUpperInvariant()) == 0)
                    {
                        // Make a note of the input ID (long), we need it for setting the Aux out.
                        _clipInputId = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeExternal) == _BMDSwitcherPortType.bmdSwitcherPortTypeExternal)
                        {
                            countDownInputOK = true;
                        }
                    }
                    // Is this the MP1 input?
                    else if (string.Compare(shortName.ToUpperInvariant(), LocalConfiguration.Settings.InputMP1.ToUpperInvariant()) == 0)
                    {
                        // Make a note of the input ID (long), we need it for setting the Aux out.
                        _inputMp1Id = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill) == _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill)
                        {
                            mp1InputOK = true;
                        }
                    }
                    // Is this the MP2 input?
                    else if (string.Compare(shortName.ToUpperInvariant(), LocalConfiguration.Settings.InputMP2.ToUpperInvariant()) == 0)
                    {
                        // Make a note of the input ID (long), we need it for setting the Aux out.
                        _inputMp2Id = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill) == _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill)
                        {
                            mp2InputOK = true;
                        }
                    }
                    // Is this the Aux Output?
                    else if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeAuxOutput) == _BMDSwitcherPortType.bmdSwitcherPortTypeAuxOutput)
                    {
                        // We need the reference to the actual port not just the long ID since we will be setting the input value.
                        _auxOut = (IBMDSwitcherInputAux)input;
                        auxOutputOK = true;
                    }
                    // Is this the Program Output?
                    else if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput) == _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput
                            && string.Compare(shortName.ToUpperInvariant(), "PGM") == 0)
                    {
                        _pgmOutId = inputId;
                        pgmOutputOK = true;
                    }
                    // Is this the Preview Output?
                    else if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput) == _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput
                            && string.Compare(shortName.ToUpperInvariant(), "PVW") == 0)
                    {
                        _pvwOutId = inputId;
                        pvwOutputOK = true;
                    }
                }
            }
            catch (COMException)
            {
                countDownInputOK = false;
            }

            // Show the status of various ATEM connections and input discoveries...
            CountdownInputStatusLed.SelectedColour = countDownInputOK ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            MP1StatusLed.SelectedColour = mp1InputOK ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            MP2StatusLed.SelectedColour = mp2InputOK ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            AuxOutStatusLed.SelectedColour = auxOutputOK ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            PvwOutStatusLed.SelectedColour = pvwOutputOK ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            PgmOutStatusLed.SelectedColour = pgmOutputOK ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            // Do the overall connection.
            if (IsAtemConnected)
            {
                AtemApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
            }
            else
            {
                // If we don't have a connection then the other items are indeterminate, so change the colour accordingly.
                AtemApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                CountdownInputStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                MP1StatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                MP2StatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                AuxOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                PvwOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                PgmOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            }
            AtemApiStatusLed.IsOn = true;   // Not is connected. Always on once we know one way or the other.
            CountdownInputStatusLed.IsOn = IsAtemConnected;
            MP1StatusLed.IsOn = IsAtemConnected;
            MP2StatusLed.IsOn = IsAtemConnected;
            AuxOutStatusLed.IsOn = IsAtemConnected;
            PvwOutStatusLed.IsOn = IsAtemConnected;
            PgmOutStatusLed.IsOn = IsAtemConnected;

            // Do the overall readiness light...
            SwitcherReadinessLED.IsOn = true;
            if (AtemApiStatusLed.SelectedColour == LightEmittingDiode.ColourOptions.Red
                || !(auxOutputOK && pvwOutputOK && pgmOutputOK && countDownInputOK))
            {
                SwitcherReadinessLED.SelectedColour = LightEmittingDiode.ColourOptions.Red;

            }
            else if (!mp1InputOK || !mp2InputOK)
            {
                SwitcherReadinessLED.SelectedColour = LightEmittingDiode.ColourOptions.Yellow;

            }
            else
            {
                SwitcherReadinessLED.SelectedColour = LightEmittingDiode.ColourOptions.Green;
            }

            StyleRunButton();
            SetOnAirDisplay();
        }

        /// <summary>
        /// Get a list of Switcher Inputs - DANGER WILL ROBINSON: Don't invoke this if _atem is null!
        /// </summary>
        public IEnumerable<IBMDSwitcherInput> SwitcherInputs
        {
            get
            {
                // Create an input iterator
                _atem.CreateIterator(typeof(IBMDSwitcherInputIterator).GUID, out IntPtr inputIteratorPtr);
                if (Marshal.GetObjectForIUnknown(inputIteratorPtr) is not IBMDSwitcherInputIterator inputIterator)
                {
                    yield break;
                }
                // Scan through all inputs
                while (true)
                {
                    inputIterator.Next(out IBMDSwitcherInput input);
                    if (input != null)
                    {
                        yield return input;
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load the current settings and display them in the Options tab...
            LoadSettings();
            ShowOptions();
            ConnectToAtem();

            // Select the most appropriate start time based on the current clock on startup
            CultureInfo enCA = new("en-CA");
            DateTime nextEveningService = DateTime.MinValue;
            DateTime nextMorningService = DateTime.MinValue;
            if (DateTime.TryParseExact(LocalConfiguration.Settings.EveningStart, "HH:mm", enCA, DateTimeStyles.None, out DateTime thisEvening))
            {
                // Are we past this time today?
                if (DateTime.Compare(DateTime.Now, thisEvening) > 0)
                {
                    // We're starting at this time tomorrow...
                    thisEvening = thisEvening.AddDays(1);
                }
                nextEveningService = thisEvening;
            }
            if (DateTime.TryParseExact(LocalConfiguration.Settings.MorningStart, "HH:mm", enCA, DateTimeStyles.None, out DateTime thisMorning))
            {
                // Are we past this time today?
                if (DateTime.Compare(DateTime.Now, thisMorning) > 0)
                {
                    // We're starting at this time tomorrow...
                    thisMorning = thisMorning.AddDays(1);
                }
                nextMorningService = thisMorning;
            }
            if (DateTime.Compare(nextEveningService,nextMorningService)<0)
            {
                ServiceStartEvening.IsChecked = true;
            }
            else
            {
                ServiceStartMorning.IsChecked = true;
            }

            // Adjust the size, position and mode of the preview window according to the settings...
            if (LocalConfiguration.Settings.RestoreWindowLocation)
            {
                _countdownViewer.Top = LocalConfiguration.Settings.WindowLocation.Y;
                _countdownViewer.Left = LocalConfiguration.Settings.WindowLocation.X;
                _countdownViewer.Height = LocalConfiguration.Settings.WindowSize.Height;
                _countdownViewer.Width = LocalConfiguration.Settings.WindowSize.Width;

                RestoreWindowLocationCheckbox.IsChecked = true;

                if (LocalConfiguration.Settings.FullScreenViewer)
                {
                    FullScreenViewerCheckBox.IsChecked = true;
                    _countdownViewer.SetViewerMode(CountdownViewer.ViewerMode.Fullscreen);
                }
                else
                {
                    FullScreenViewerCheckBox.IsChecked = false;
                    _countdownViewer.SetViewerMode(CountdownViewer.ViewerMode.Normal);
                }
            }

            // Figure out the next on-air time...
            DetermineNextStartTime();
            // Start minding the time...
            _heartbeat.Start();
            _loaded = true;
        }

        /// <summary>
        /// Figure out the next on-air time (must be a point in the future)
        /// </summary>
        private void DetermineNextStartTime()
        {
            _OnAirTime = DateTime.MinValue;
            
            CultureInfo enCA = new("en-CA");
            string timeToTry = string.Empty;

            if ((bool)ServiceStartEvening.IsChecked)
            {
                // Next start is an evening service.
                timeToTry = LocalConfiguration.Settings.EveningStart;
            }
            else if ((bool)ServiceStartMorning.IsChecked)
            {
                // Next start is a morning service.
                timeToTry = LocalConfiguration.Settings.MorningStart;
            }
            else if ((bool)ServiceStartCustom.IsChecked)
            {
                // Next start is at a custom time, as long as a valid one is provided.
                if (IsValidTimeOfDayString(CustomStartTimeTextBox.Text, out string validCustomStartTime))
                {
                    timeToTry = validCustomStartTime;
                }
            }
            // Do we have a good starting time?
            if (DateTime.TryParseExact(timeToTry, "HH:mm", enCA, DateTimeStyles.None, out DateTime timeOfDayToday))
            {
                // Are we past this time today?
                if (DateTime.Compare(DateTime.Now, timeOfDayToday) > 0)
                {
                    // We're starting at this time tomorrow...
                    timeOfDayToday = timeOfDayToday.AddDays(1);
                }
                _OnAirTime = timeOfDayToday;
            }
        }

        /// <summary>
        /// Display the current configuration options on the Options tab
        /// </summary>
        private void ShowOptions()
        {
            AtemIpAddressTextBox.Text = LocalConfiguration.Settings.AtemIpAddress;
            CountdownInputTextBox.Text = LocalConfiguration.Settings.InputCountdown;
            MP1InputTextBox.Text = LocalConfiguration.Settings.InputMP1;
            MP2InputTextBox.Text = LocalConfiguration.Settings.InputMP2;
            EveningServiceStartTimeTextBox.Text = LocalConfiguration.Settings.EveningStart;
            MorningServiceStartTimeTextBox.Text = LocalConfiguration.Settings.MorningStart;
            WallpaperFilespecTextBox.Text = LocalConfiguration.Settings.WallpaperFilespec;
            XPositionTextBox.Text = LocalConfiguration.Settings.XPosition.ToString();
            YPositionTextBox.Text = LocalConfiguration.Settings.YPosition.ToString();
            TurnGreenTextBox.Text = LocalConfiguration.Settings.TurnGreen;
            TurnYellowTextBox.Text = LocalConfiguration.Settings.TurnYellow;
            FontSizeTextBox.Text = LocalConfiguration.Settings.FontSize.ToString();

            // Other places they show up...
            ServiceStartEvening.Content = "Evening " + LocalConfiguration.Settings.EveningStart;
            ServiceStartMorning.Content = "Morning " + LocalConfiguration.Settings.MorningStart;
            CustomStartTimeTextBox.Text = LocalConfiguration.Settings.LastCustomStart;

            // Apply the options as well...
            _countdownViewer.SetWallpaperImage(LocalConfiguration.Settings.WallpaperFilespec);
            _countdownViewer.SetBaseFontSize(LocalConfiguration.Settings.FontSize);
            _countdownViewer.SetHorizontalMargin(LocalConfiguration.Settings.XPosition);
            _countdownViewer.SetBottomMargin(LocalConfiguration.Settings.YPosition);

            SetOnAirDisplay();
        }

        /// <summary>
        /// Configure the UI elements of the ON AIR sign based on whether we are on air or not
        /// </summary>
        private void SetOnAirDisplay()
        {
            if (IsOnAir() && SuppressOnAirNotificationCheckbox.IsChecked == false)
            {
                _countdownViewer.SetOnAir(true);
                OnAirLightBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.PlayingMainBrush);
                OnAirLightBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.PlayingBackgroundBrush);
                OnAirLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PlayingMainBrush);
            }
            else
            {
                _countdownViewer.SetOnAir(false);
                OnAirLightBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
                OnAirLightBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.DisabledBackgroundBrush);
                OnAirLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            }
        }

        private void ServiceStartEvening_Checked(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                DetermineNextStartTime();
            }
        }

        private void ServiceStartMorning_Checked(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                DetermineNextStartTime();
            }
        }

        private void ServiceStartCustom_Checked(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                DetermineNextStartTime();
            }
        }

        private void CustomStartTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loaded)
            {
                DetermineNextStartTime();
                if (CustomStartTimeTextBox.Text.Trim() != LocalConfiguration.Settings.LastCustomStart)
                {
                    // Is the new value valid?
                    if (IsValidTimeOfDayString(CustomStartTimeTextBox.Text, out string formattedTimeOfDay))
                    {
                        LocalConfiguration.Settings.LastCustomStart = formattedTimeOfDay;
                        LocalConfiguration.Save();
                    }
                }
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleRunningStatus(RunAction.Toggle);
        }

        /// <summary>
        /// Configure the ATEM based on the indicated run action
        /// </summary>
        /// <param name="runAction">Choose whether to run, stop or toggle the state of the ATEM auxiliary output</param>
        private void ToggleRunningStatus(RunAction runAction)
        {
            bool currentlyRunning = false;
            if (_auxOut != null)
            {
                _auxOut.GetInputSource(out long auxInput);
                currentlyRunning = (auxInput == _clipInputId);
            }

            if (((runAction == RunAction.Toggle) && currentlyRunning) || (runAction == RunAction.ForceStop))
            {
                _auxOut.SetInputSource(_pgmOutId);
            }
            else if (((runAction == RunAction.Toggle) && !currentlyRunning) || (runAction == RunAction.ForceRun))
            {
                _auxOut.SetInputSource(_clipInputId);
            }
            // Otherwise we're already good.

            if (runAction == RunAction.ForceRun)
            {
                currentlyRunning = true;
            }
            else if (runAction == RunAction.ForceStop)
            {
                currentlyRunning = false;
            }
            else // runAction == RunAction.Toggle
            {
                currentlyRunning = !currentlyRunning;
            }
            StyleRunButton(currentlyRunning);
        }

        /// <summary>
        /// Set the appearance of the Show/Hide Countdown button depending on the current operating mode
        /// </summary>
        private void StyleRunButton()
        {
            bool currentlyRunning = false;
            if (_auxOut != null)
            {
                _auxOut.GetInputSource(out long auxInput);
                currentlyRunning = (auxInput == _clipInputId);
            }
            StyleRunButton(currentlyRunning);
        }

        /// <summary>
        /// Set the appearance of the Show/Hide Countdown button depending on the current operating mode
        /// </summary>
        /// <param name="currentlyRunning">Use True if the countdown is being displayed currently, false otherwise</param>
        private void StyleRunButton(bool currentlyRunning)
        {
            if (currentlyRunning)
            {
                RunButton.Content = "     Hide\nCountdown";
                RunButton.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PlayingContrastBrush);
                RunButton.Background = AppResources.DefinedColour(AppResources.StaticResource.PlayingMainBrush);
            }
            else
            {
                RunButton.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CuedContrastBrush);
                RunButton.Background = AppResources.DefinedColour(AppResources.StaticResource.CuedMainBrush);
                RunButton.Content = "     Show\nCountdown";
            }
        }

        private void Heartbeat_Tick(object sender, EventArgs e)
        {
            // Update the time displays
            CurrentTimeMessage.Text = DateTime.Now.ToString("HH:mm:ss");

            // How long until the next service start?
            TimeSpan tMinus = TimeSpan.FromSeconds((_OnAirTime - DateTime.Now).TotalSeconds + 1);
            TimeToAirMessage.Text = tMinus.ToString("hh\\:mm\\:ss");
            // Show it on the viewer.
            _countdownViewer.SetCountdownTime(tMinus);

            // Set the colour according to the time remaining.
            if (DateTime.Compare(DateTime.Now,_OnAirTime)==1)
            {
                // When the clock runs down, stop showing the countdown.
                TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PlayingMainBrush);
                _countdownViewer.SetCountdownColour(CountdownViewer.CountdownColour.Red);

                ToggleRunningStatus(RunAction.ForceStop);
            }
            else if ((int)tMinus.TotalSeconds <= SecondsFromMinutesAndSecondsString(LocalConfiguration.Settings.TurnYellow))
            {
                TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CountdownWarningBrush);
                _countdownViewer.SetCountdownColour(CountdownViewer.CountdownColour.Yellow);
            }
            else if ((int)tMinus.TotalSeconds <= SecondsFromMinutesAndSecondsString(LocalConfiguration.Settings.TurnGreen))
            {
                TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CuedMainBrush);
                _countdownViewer.SetCountdownColour(CountdownViewer.CountdownColour.Green);
            }
            else
            {
                TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.TextBoxForegroundBrush);
                _countdownViewer.SetCountdownColour(CountdownViewer.CountdownColour.White);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If we're showing the countdown, then take that down so as not to leave things hanging...
            ToggleRunningStatus(RunAction.ForceStop);

            // Save viewer window settings before closing...
            if (RestoreWindowLocationCheckbox.IsChecked?? false == true)
            {
                // Note the size, position and mode of the viewer window...
                LocalConfiguration.Settings.WindowLocation = new System.Drawing.Point
                    (Convert.ToInt32(_countdownViewer.Left), Convert.ToInt32(_countdownViewer.Top));
                LocalConfiguration.Settings.WindowSize = new System.Drawing.Size
                    (Convert.ToInt32(_countdownViewer.Width), Convert.ToInt32(_countdownViewer.Height));
                LocalConfiguration.Settings.FullScreenViewer = FullScreenViewerCheckBox.IsChecked ?? false;
            }
            LocalConfiguration.Settings.RestoreWindowLocation = RestoreWindowLocationCheckbox.IsChecked ?? false;
            LocalConfiguration.Settings.SuppressOnAirNotification = SuppressOnAirNotificationCheckbox.IsChecked ?? false;
            LocalConfiguration.Save();

            _countdownViewer.ShutDownViewer();
        }

        private void FullScreenViewerCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                _countdownViewer.SetViewerMode(CountdownViewer.ViewerMode.Fullscreen);
            }
        }

        private void FullScreenViewerCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_loaded)
            {
                _countdownViewer.SetViewerMode(CountdownViewer.ViewerMode.Normal);
            }
        }

        private void RefreshAtemConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectToAtem();
        }

        private void ChooseWallpaperButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new()
            {
                AddExtension = true,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "JPG|*.jpg|PNG|*.png|All Files|*.*",
                Multiselect = false,
                Title = "TimeToAir - Choose Wallpaper Image"
            };
            if ((bool)dlg.ShowDialog())
            {
                WallpaperFilespecTextBox.Text = dlg.FileName;
            }
        }

        /// <summary>
        /// Determines whether the ATEM Program source is one of the media players or not
        /// </summary>
        /// <returns>True when the program source is something other than a media player</returns>
        private bool IsOnAir()
        {
            bool result = false;

            if (IsAtemConnected && _me0 != null)
            {
                _me0.GetProgramInput(out long currentPgmOut);
                if (!(currentPgmOut > 0 &&
                   (( currentPgmOut == _inputMp1Id && _inputMp1Id > 0)
                   || (currentPgmOut == _inputMp2Id && _inputMp2Id > 0))))
                {
                    result = true;
                }
            }

            return result;
        }

        private void RestoreWindowLocationCheckbox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void RestoreWindowLocationCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {

        }

        private void SuppressOnAirNotificationCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            SetOnAirDisplay();
        }

        private void SuppressOnAirNotificationCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetOnAirDisplay();
        }
    }
}


