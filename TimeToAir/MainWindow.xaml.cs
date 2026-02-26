using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mooseware.Tachnit.BmdHyperdeckController;
using Mooseware.Tachnit.PtzOpticsCameraController;
using Mooseware.Tachnit.WebPresenterApi;
using Mooseware.TimeToAir.Configuration;
using Mooseware.TimeToAir.Controls;
using Mooseware.TimeToAir.Themes.Styles;

namespace Mooseware.TimeToAir;

// TODO: Plan for a v2.0 release with all of these features and tasks...
// TODO: Implement remaining feature flags:
//       - UseYouTubeConnection
// TODO: Test the snot out of the auto-start actions that will largely replace the 2nd StreamDeck screen
// TODO: Implement the remaining v2.0 automationfeatures
//       - Check the YouTube API and get the event set up if it isn't already
//         +-> YT app key can go in the app settings.
// TODO: Convert BMD API local loggers to full app logger (maybe keep local if DI comes up empty)
// TODO: Add logging into places that don't have it yet.

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
    /// Reference to the ATEM video switcher API
    /// </summary>
    private Switcher _atem = null;

    /// <summary>
    /// Reference to the HyperDeck recorder API
    /// </summary>
    private HyperdeckController _hyperDeck = null;

    /// <summary>
    /// Reference to the WebPresenter recorder
    /// </summary>
    private WebPresenterController _webPresenter = null;

    /// <summary>
    /// Date and time of the start of the next service. This is the point in time we are counting down to.
    /// </summary>
    private DateTime _onAirTime = DateTime.MinValue;

    /// <summary>
    /// The (calculated) time at which streaming should be started automatically (in auto mode)
    /// </summary>
    private DateTime _streamStartTime = DateTime.MaxValue;

    /// <summary>
    /// The (calculated) time at which recording should be started automatically (in auto mode)
    /// </summary>
    private DateTime _startRecordingTime = DateTime.MaxValue;

    /// <summary>
    /// The (calculated) time at which the secondary camera should be previewed automatically (in auto mode)
    /// </summary>
    private DateTime _secondaryCameraPreviewTime = DateTime.MaxValue;

    /// <summary>
    /// The (calculated time at which the app is automatically shut down (after going live)
    /// </summary>
    private DateTime _autoShutDownTime = DateTime.MaxValue;

    /// <summary>
    /// The number of the camera which should be previewed shortly after going live
    /// </summary>
    private int _secondaryCamera = 0;

    /// <summary>
    /// Sentinel to note that streaming has been started automatically (so stop trying)
    /// </summary>
    private bool _startedStreaming = false;

    /// <summary>
    /// Sentinel to note that recording has been started automatically (so stop trying)
    /// </summary>
    private bool _startedRecording = false;

    /// <summary>
    /// Sentinel to note that the stream has gone on-air automatically (so stop trying)
    /// </summary>
    private bool _goneOnAir = false;

    /// <summary>
    /// Sentinel to note that the secondary camera has been sent to Preview (so stop trying)
    /// </summary>
    private bool _previewedSecondaryCamera = false;

    /// <summary>
    /// The title of the next livestream event
    /// </summary>
    private string _nextEventTitle = "Next Livestream";

    /// <summary>
    /// The subtitle (if any) of the next livestream event
    /// </summary>
    private string _nextEventSubtitle = "(TBD)";

    /// <summary>
    /// The scheduled time after which a full suite of connection tests should be performed.
    /// When tests are not pending DateTime.MaxValue is used.
    /// </summary>
    private DateTime _pendingTestStart = DateTime.MaxValue;

    /// <summary>
    /// The last clock time that a full suite of connection tests was completed.
    /// When tests have never been done DateTime.MinValue is used.
    /// </summary>
    private DateTime _lastTestFinish = DateTime.MinValue;

    /// <summary>
    /// Sentinel to prevent reentrancy while testing.
    /// </summary>
    private bool _activelyTesting = false;

    /// <summary>
    /// True when the last completed test resulted in a caution status.
    /// False when Ready, Testing or TBD (e.g. otherwise)
    /// </summary>
    private bool _inCautionStatus = false;

    /// <summary>
    /// Sentinel to prevent null references to controls while loading the form.
    /// </summary>
    private bool _loaded = false;   // Sentinel to prevent null refs while loading the window.

    /// <summary>
    /// Sentinel to track whether PTZ camera presets have been applied
    /// (After startup and first test, but not after _every_ test(!)
    /// </summary>
    private bool _ptzSetupApplied = false;

    /// <summary>
    /// Window where the countdown time and on air notice are shown
    /// </summary>
    private readonly CountdownViewer _countdownViewer = new();

    /// <summary>
    /// Flag for whether or not the ATEM connection test has succeeded
    /// </summary>
    private bool IsAtemConnected { get; set; } = false;

    /// <summary>
    /// Flag for whether or not the HyperDeck connection test has succeeded
    /// </summary>
    private bool IsHyperDeckConnected { get; set; } = false;

    /// <summary>
    /// Last known remaining HyperDeck recording capacity in minutes
    /// </summary>
    private int RecordingMinutesRemaining { get; set; } = 0;

    /// <summary>
    /// Flag for whether or not the WebPresenter streaming bridge is connected
    /// </summary>
    private bool IsWebPresenterConnected { get; set; } = false;

    /// <summary>
    /// Flag for whether or not the Camera 1 PTZ camera is connected
    /// </summary>
    private bool IsCam1Connected { get; set; } = false;

    /// <summary>
    /// Flag for whether or not the Camera 2 PTZ camera is connected
    /// </summary>
    private bool IsCam2Connected { get; set; } = false;

    /// <summary>
    /// Flag tracking whether or not the Schedule API connection has succeeded 
    /// </summary>
    private bool IsScheduleConnected { get; set; } = false;

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

    /// <summary>
    /// Application settings loaded from the appsettings.json file at application startup
    /// </summary>
    private readonly AppSettings _appSettings;

    /// <summary>
    /// Logging instance established at the application start
    /// </summary>
    private readonly ILogger<MainWindow> _logger;

    /// <summary>
    /// Http Client Factory from DI for use by components that need one (e.g. WebPresenterController)
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;

    public MainWindow(IOptions<Configuration.AppSettings> appSettings, ILogger<MainWindow> logger, IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();

        // Get a DI reference to the appsettings.json configuration data
        _appSettings = appSettings.Value;

        // Get a DI reference to the IHttpClientFactory
        _httpClientFactory = httpClientFactory;

        // Get a DI reference to the (Serilog) Logger
        _logger = logger;
        _logger.LogInformation("MainWindow initializing");

        _heartbeat = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _heartbeat.Tick += Heartbeat_Tick;

        Config.User.Reload();

        _countdownViewer.Show();
    }

    private void ApplyOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        bool atemReconnectRequired = false; // or any other API object needs to be re-initialized.
        bool anyOtherSettingChanged = false;
        string formattedTimeSpan;

        if (AtemIpAddressTextBox.Text.Trim() != Config.User.AtemIpAddress)
        {
            Config.User.AtemIpAddress = AtemIpAddressTextBox.Text.Trim();
            atemReconnectRequired = true;
        }
        if (CountdownInputTextBox.Text.Trim() != Config.User.InputCountdownName)
        {
            Config.User.InputCountdownName = CountdownInputTextBox.Text.Trim();
            atemReconnectRequired = true;
        }
        if (TitleCardInputTextBox.Text.Trim() != Config.User.InputTitleCardName)
        {
            Config.User.InputTitleCardName = TitleCardInputTextBox.Text.Trim();
            atemReconnectRequired = true;
        }
        if (WallpaperFilespecTextBox.Text.Trim() != Config.User.WallpaperFilespec)
        {
            Config.User.WallpaperFilespec = WallpaperFilespecTextBox.Text.Trim();
            atemReconnectRequired = true;
        }
        if (int.TryParse(XPositionTextBox.Text.Trim(), out int newXPosition))
        {
            if (XPositionTextBox.Text.Trim() != Config.User.XPosition.ToString())
            {
                Config.User.XPosition = newXPosition;
                atemReconnectRequired = true;
            }
        }
        if (int.TryParse(YPositionTextBox.Text.Trim(), out int newYPosition))
        {
            if (YPositionTextBox.Text.Trim() != Config.User.YPosition.ToString())
            {
                Config.User.YPosition = newYPosition;
                atemReconnectRequired = true;
            }
        }
        if (TurnGreenTextBox.Text.Trim() != Config.User.TurnGreen)
        {
            // Is the new value valid?
            if (IsValidMinutesAndSeconds(TurnGreenTextBox.Text, out formattedTimeSpan))
            {
                Config.User.TurnGreen = formattedTimeSpan;
                anyOtherSettingChanged = true;
            }
        }
        if (TurnYellowTextBox.Text.Trim() != Config.User.TurnYellow)
        {
            // Is the new value valid?
            if (IsValidMinutesAndSeconds(TurnYellowTextBox.Text, out formattedTimeSpan))
            {
                Config.User.TurnYellow = formattedTimeSpan;
                anyOtherSettingChanged = true;
            }
        }
        if (int.TryParse(FontSizeTextBox.Text.Trim(), out int newFontSize))
        {
            if (FontSizeTextBox.Text.Trim() != Config.User.FontSize.ToString())
            {
                Config.User.FontSize = newFontSize;
                anyOtherSettingChanged = true;
            }
        }

        if (atemReconnectRequired || anyOtherSettingChanged)
        {
            // Save settings changes...
            Config.User.Save();
            ////LocalConfiguration.Save();

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

    private static string MinutesAndSecondsStringFromTotalSeconds(int seconds)
    {
        int wholeMinutes = (int)(seconds / 60);
        if (wholeMinutes >= 100)
        {
            wholeMinutes = 99;  // Arbitrary maximum.
        }
        int remainingSeconds = seconds % 60;

        return wholeMinutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }

    /// <summary>
    /// Connect to the ATEM API based on current settings and test the availability of required inputs
    /// </summary>
    private void ConnectToAtem()
    {
        IsAtemConnected = false;

        // If this is a reconnection, get rid of the old references first
        if (_atem is not null)
        {
            _atem = null;
        }

        if (_appSettings.UseAtemConnection)
        {
            _atem = new(
                atemIpAddress: Config.User.AtemIpAddress,
                input1: Config.User.InputCam1Name,
                input2: Config.User.InputCam2Name,
                inputTitleCard: Config.User.InputTitleCardName,
                inputCountdown: Config.User.InputCountdownName);

            IsAtemConnected = _atem.IsReady;

            // Show the status of various ATEM connections and input discoveries...
            CountdownInputStatusLed.SelectedColour = _atem.CountdownInputReady ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            TitleCardStatusLed.SelectedColour = _atem.InputTitleCardReady ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            Cam1StatusLed.SelectedColour = _atem.Input1Ready ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            Cam2StatusLed.SelectedColour = _atem.Input2Ready ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            AuxOutStatusLed.SelectedColour 
                = _atem.AuxOutReady || !_appSettings.UseAuxOut 
                ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            PvwOutStatusLed.SelectedColour = _atem.PvwOutReady ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
            PgmOutStatusLed.SelectedColour = _atem.PgmOutReady ? LightEmittingDiode.ColourOptions.Green : LightEmittingDiode.ColourOptions.Red;
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
                TitleCardStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                Cam1StatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                Cam2StatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                AuxOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                PvwOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                PgmOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            }
            AtemApiStatusLed.IsOn = true;   // Not is connected. Always on once we know one way or the other.
        }
        else
        {
            // Not using the ATEM connection
            AtemApiStatusLed.IsOn = false;
            AtemApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            CountdownInputStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            TitleCardStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            Cam1StatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            Cam2StatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            AuxOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            PvwOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
            PgmOutStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
        }

        CountdownInputStatusLed.IsOn = IsAtemConnected;
        TitleCardStatusLed.IsOn = IsAtemConnected;
        Cam1StatusLed.IsOn = IsAtemConnected;
        Cam2StatusLed.IsOn = IsAtemConnected;
        AuxOutStatusLed.IsOn = IsAtemConnected;
        PvwOutStatusLed.IsOn = IsAtemConnected;
        PgmOutStatusLed.IsOn = IsAtemConnected;

        StyleRunButton();
        SetOnAirDisplay();
    }

    private async Task ConnectToHyperDeck()
    {
        IsHyperDeckConnected = false;

        // If this is a reconnection, get rid of the old reference first.
        if (_hyperDeck is not null)
        {
            _hyperDeck = null;
        }

        if (_appSettings.UseHyperDeckConnection)
        {
            _hyperDeck = new(Config.User.HyperDeckIpAddress);
            try
            {
                IsHyperDeckConnected = await _hyperDeck.TestConnectionIsOKAsync();

                if (IsHyperDeckConnected)
                {
                    HyperDeckApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
                    HyperDeckApiStatusLed.IsOn = true;
                    RecordingMinutesRemaining = await _hyperDeck.GetRemainingCapacityInMinutesAsync();
                    RecordingCapacityTextBox.Text = RecordingMinutesRemaining.ToString();
                    RecordingCapacityStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
                    if (RecordingMinutesRemaining <= _appSettings.RecordingMinutesYellow)
                    {
                        RecordingCapacityStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Yellow;
                    }
                    if (RecordingMinutesRemaining <= _appSettings.RecordingMinutesRed)
                    {
                        RecordingCapacityStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                    }
                    RecordingCapacityStatusLed.IsOn = true;
                    switch (_hyperDeck.Slot1Status)
                    {
                        case StorageSlotStatus.Unknown:
                            SdCard1SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Yellow;
                            break;
                        case StorageSlotStatus.NotFull:
                            SdCard1SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
                            break;
                        case StorageSlotStatus.Full:
                            SdCard1SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                            break;
                        default:
                            break;
                    }
                    SdCard1SpaceStatusLed.IsOn = true;
                    switch (_hyperDeck.Slot2Status)
                    {
                        case StorageSlotStatus.Unknown:
                            SdCard2SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Yellow;
                            break;
                        case StorageSlotStatus.NotFull:
                            SdCard2SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
                            break;
                        case StorageSlotStatus.Full:
                            SdCard2SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                            break;
                        default:
                            break;
                    }
                    SdCard2SpaceStatusLed.IsOn = true;
                }
                else
                {
                    HyperDeckApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                    HyperDeckApiStatusLed.IsOn = true;
                    SdCard1SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                    SdCard1SpaceStatusLed.IsOn = false;
                    SdCard2SpaceStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                    SdCard2SpaceStatusLed.IsOn = false;
                    RecordingCapacityTextBox.Text = "TBD";
                    RecordingCapacityStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                    RecordingCapacityStatusLed.IsOn = false;
                }
            }
            catch (Exception)
            {
                // There is no HyperDeck connection available
            }
        }
    }

    private async Task ConnectToWebPresenter()
    {
        IsWebPresenterConnected = false;

        // If this is a reconnection, get rid of the old reference first.
        if (_webPresenter is not null)
        {
            _webPresenter = null;
        }

        if (_appSettings.UseWebPresenterConnection)
        {
            _webPresenter = new(Config.User.WebPresenterIpAddress, _httpClientFactory);
            try
            {
                IsWebPresenterConnected = await _webPresenter.Ping();
                if (IsWebPresenterConnected)
                {
                    WebPresenterApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
                }
                else
                {
                    WebPresenterApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                }
                WebPresenterApiStatusLed.IsOn = true;
            }
            catch (Exception)
            {
                // There is no WebPresenter connection available
            }
        }
    }

    private void ConnectToPtzCameras()
    {
        IsCam1Connected = false;
        IsCam2Connected = false;

        PtzCameraController ptzCam;
        if (_appSettings.UsePtzCameraConnections)
        {
            try
            {
                ptzCam = new(Config.User.Camera1IpAddress);
                IsCam1Connected = ptzCam.IsConnected;
                ptzCam = null;

                ptzCam = new(Config.User.Camera2IpAddress);
                IsCam2Connected = ptzCam.IsConnected;
                ptzCam = null;
            }
            catch (Exception)
            {
                // No PTZ Cam connection
            }
            if (IsCam1Connected)
            {
                Camera1ConnectionStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
            }
            else
            {
                Camera1ConnectionStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
            }
            Camera1ConnectionStatusLed.IsOn = true;
            if (IsCam2Connected)
            {
                Camera2ConnectionStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
            }
            else
            {
                Camera2ConnectionStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
            }
            Camera2ConnectionStatusLed.IsOn = true;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Load the current settings and display them in the Options tab...
        ShowOptions();
        SetGoLiveModeIndicatorDisplay();

        // Adjust the size, position and mode of the preview window according to the settings...
        if (Config.User.RestoreWindowLocation)
        {
            _countdownViewer.Top = Config.User.WindowLocation.Y;
            _countdownViewer.Left = Config.User.WindowLocation.X;
            _countdownViewer.Height = Config.User.WindowSize.Height;
            _countdownViewer.Width = Config.User.WindowSize.Width;

            RestoreWindowLocationCheckbox.IsChecked = true;

            if (Config.User.FullScreenViewer)
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

        // Show the current application version number and copyright notice.
        string copyrightNotice = string.Empty;
        var attribs = Assembly.GetEntryAssembly()?.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
        if (attribs?.Length > 0)
        {
            copyrightNotice = ((AssemblyCopyrightAttribute)attribs[0]).Copyright;
        }
        AppVersionTextBlock.Text = "Version: "
            + Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString()
            + "  "
            + copyrightNotice;

        // Give the app a couple of seconds to paint and then do a full round of connection tests
        _pendingTestStart = DateTime.Now.AddSeconds(_appSettings.SecondsToWaitForTesting);

        ShowTestStatus();

        RunAction showHideAction = Config.User.CountdownShown ? RunAction.ForceRun : RunAction.ForceStop;
        SetOnAirDisplay();
        ToggleRunningStatus(showHideAction);

        // And start minding the time...
        _heartbeat.Start();
        _loaded = true;
    }

    /// <summary>
    /// Figure out the next on-air time (must be a point in the future)
    /// </summary>
    private void DetermineNextStartTime()
    {
        _onAirTime = DateTime.MinValue;
        IsScheduleConnected = false;

        bool sourceIsApi = false;

        if (_appSettings.ForceTestCountdownSeconds > 0)
        {
            // appsettings.json includes a forced countdown value for testing purposes.
            // This overrides any other consideration for determining On Air Time
            _onAirTime = DateTime.Now.AddSeconds(_appSettings.ForceTestCountdownSeconds);
            _nextEventTitle = "Test Event";
            _nextEventSubtitle = "Forced Start Time";
        }
        else if (_appSettings.UseScheduleApiConnection)
        {
            string streamDate = string.Empty;
            string startTime = string.Empty;
            // Get the next start time based on the schedule API
            var web = new HtmlWeb();
            var doc = web.Load(_appSettings.EventScheduleUrl);
            if (doc != null)
            {
                // Get the next livestream details: Title, Subtitle, Start Date and Time
                streamDate = doc.GetElementbyId("streamdate")?.InnerText ?? string.Empty;
                startTime = doc.GetElementbyId("starttime")?.InnerText ?? string.Empty;
                _nextEventTitle = doc.GetElementbyId("streamtitle")?.InnerText ?? "Schedule unavailable";
                _nextEventSubtitle = doc.GetElementbyId("streamsubtitle")?.InnerText ?? string.Empty;
            }

            // Can we parse a date and time out of the values retrieved from the API?
            if (DateTime.TryParseExact(streamDate + " " + startTime, "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTime parsedDateTime))
            {
                _onAirTime = parsedDateTime;
                EventScheduleApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Green;
                IsScheduleConnected = true;
                sourceIsApi = true;
            }
            else
            {
                EventScheduleApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
            }
            EventScheduleApiStatusLed.IsOn = true;
        }

        // If it didn't work or if we're not using the schedule API, guess morning/evening based on current clock
        if (_onAirTime < DateTime.Now)
        {
            DateTime nextEveningService = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 20, 0, 0);
            // Are we past this time today?
            if (DateTime.Compare(DateTime.Now, nextEveningService) > 0)
            {
                // We're starting at this time tomorrow...
                nextEveningService = nextEveningService.AddDays(1);
            }
            DateTime nextMorningService = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 10, 0, 0);
            // Are we past this time today?
            if (DateTime.Compare(DateTime.Now, nextMorningService) > 0)
            {
                // We're starting at this time tomorrow...
                nextMorningService = nextMorningService.AddDays(1);
            }
            if (DateTime.Compare(nextEveningService, nextMorningService) < 0)
            {
                _onAirTime = nextEveningService;
                _nextEventTitle = "Solel Evening Service";
                _nextEventSubtitle = string.Empty;
            }
            else
            {
                _onAirTime = nextMorningService;
                _nextEventTitle = "Solel Morning Service";
                _nextEventSubtitle = string.Empty;
            }
            if (_appSettings.UseScheduleApiConnection)
            {
                EventScheduleApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.Red;
                EventScheduleApiStatusLed.IsOn = true;
            }
            else
            {
                EventScheduleApiStatusLed.SelectedColour = LightEmittingDiode.ColourOptions.White;
                EventScheduleApiStatusLed.IsOn = false;
            }
        }

        // Show the next event details in the GUI
        string nextStreamEventDescription = _nextEventTitle 
            + (_nextEventSubtitle.Length > 0  ? " - " : string.Empty)
            + _nextEventSubtitle;
        NextStreamCountdownText.Text = nextStreamEventDescription;
        if (_onAirTime.Date != DateTime.Now.Date)
        {
            // Include the date in the description
            NextStreamCountdownText.Text += ": " + _onAirTime.ToString("dddd d MMMM");
        }

        if (sourceIsApi)
        {
            StreamEventsScheduleDetailsTextBlock.Text = nextStreamEventDescription;
        }
        else
        {
            StreamEventsScheduleDetailsTextBlock.Text = "Schedule unavailable, presuming:";
        }
        StreamEventsScheduleDetailsTextBlock.Text += " " + _onAirTime.ToString("dddd d MMMM HH:mm");

        // Once we have an answer (even if it's a guess) the UI on the main tab should show as "settled"
        NextStreamCountdownText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
        NextStreamCountdownLabelText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
    
        // Determine the other derived deadlines based on whatever has been identified thus far
        if (_onAirTime != DateTime.MinValue)
        {
            _streamStartTime = _onAirTime.AddSeconds(-1 * SecondsFromMinutesAndSecondsString(Config.User.StartStreaming));
            _startRecordingTime = _onAirTime.AddSeconds(-1 * SecondsFromMinutesAndSecondsString(Config.User.StartRecording));
            _secondaryCameraPreviewTime = _onAirTime.AddSeconds(_appSettings.SecondaryCameraPreviewDelay);
        }
        else
        {
            _streamStartTime = DateTime.MaxValue;
            _startRecordingTime = DateTime.MaxValue;
            _secondaryCameraPreviewTime = DateTime.MaxValue;
        }
    }

    /// <summary>
    /// Display the current configuration options on the Options tab
    /// </summary>
    private void ShowOptions()
    {
        // Connections Tab
        AtemIpAddressTextBox.Text = Config.User.AtemIpAddress;
        CountdownInputTextBox.Text = Config.User.InputCountdownName;
        TitleCardInputTextBox.Text = Config.User.InputTitleCardName;
        Camera1InputTextBox.Text = Config.User.InputCam1Name;
        Camera2InputTextBox.Text = Config.User.InputCam2Name;
        WebPresenterIpAddressTextBox.Text = Config.User.WebPresenterIpAddress;
        HyperDeckIpAddressTextBox.Text = Config.User.HyperDeckIpAddress;
        Camera1IpAddressTextBox.Text = Config.User.Camera1IpAddress;
        Camera2IpAddressTextBox.Text = Config.User.Camera2IpAddress;
        
        // Countdown Options Tab
        TurnGreenTextBox.Text = Config.User.TurnGreen;
        StartStreamTextBox.Text = Config.User.StartStreaming;
        TurnYellowTextBox.Text = Config.User.TurnYellow;
        StartRecordingTextBox.Text = Config.User.StartRecording;
        AutoShutdownCheckBox.IsChecked = Config.User.ShutDownAfterGoLive;
        AutoCloseTime.Text = MinutesAndSecondsStringFromTotalSeconds(_appSettings.AutoShutdownTimeout);
        WallpaperFilespecTextBox.Text = Config.User.WallpaperFilespec;
        XPositionTextBox.Text = Config.User.XPosition.ToString();
        YPositionTextBox.Text = Config.User.YPosition.ToString();
        FontSizeTextBox.Text = Config.User.FontSize.ToString();
        FullScreenViewerCheckBox.IsChecked = Config.User.FullScreenViewer;
        RestoreWindowLocationCheckbox.IsChecked = Config.User.RestoreWindowLocation;
        SuppressOnAirNotificationCheckbox.IsChecked = Config.User.SuppressOnAirNotification;
        
        // Other Options Tab
        StreamDescriptionShabbatEveningTextBox.Text = Config.User.StreamDescriptionShabbatPM;
        StreamDescriptionFestivalEveningTextBox.Text = Config.User.StreamDescriptionFestivalPM;
        StreamDescriptionShabbatMorningTextBox.Text = Config.User.StreamDescriptionShabbatAM;
        StreamDescriptionFestivalMorningTextBox.Text = Config.User.StreamDescriptionFestivalAM;
        RecordingFileNameTextBox.Text = Config.User.LocalRecordingName;
        PtzSetup1NameTextBox.Text = Config.User.PtzSetup1Name;
        PtzSetup1Cam1.IsChecked = (Config.User.PtzSetup1Preview == 1);
        PtzSetup1Cam2.IsChecked = (Config.User.PtzSetup1Preview != 1);
        PtzSetup1Cam1Preset.Text = Config.User.PtzSetup1Cam1.ToString();
        PtzSetup1Cam2Preset.Text = Config.User.PtzSetup1Cam2.ToString();
        PtzSetup2NameTextBox.Text = Config.User.PtzSetup2Name;
        PtzSetup2Cam1.IsChecked = (Config.User.PtzSetup2Preview == 1);
        PtzSetup2Cam2.IsChecked = (Config.User.PtzSetup2Preview != 1);
        PtzSetup2Cam1Preset.Text = Config.User.PtzSetup2Cam1.ToString();
        PtzSetup2Cam2Preset.Text = Config.User.PtzSetup2Cam2.ToString();
        PtzSetup3NameTextBox.Text = Config.User.PtzSetup3Name;
        PtzSetup3Cam1.IsChecked = (Config.User.PtzSetup3Preview == 1);
        PtzSetup3Cam2.IsChecked = (Config.User.PtzSetup3Preview != 1);
        PtzSetup3Cam1Preset.Text = Config.User.PtzSetup3Cam1.ToString();
        PtzSetup3Cam2Preset.Text = Config.User.PtzSetup3Cam2.ToString();
        PtzSetup4NameTextBox.Text = Config.User.PtzSetup4Name;
        PtzSetup4Cam1.IsChecked = (Config.User.PtzSetup4Preview == 1);
        PtzSetup4Cam2.IsChecked = (Config.User.PtzSetup4Preview != 1);
        PtzSetup4Cam1Preset.Text = Config.User.PtzSetup4Cam1.ToString();
        PtzSetup4Cam2Preset.Text = Config.User.PtzSetup4Cam2.ToString();
        switch (Config.User.PtzDefaultPM)
        {
            case 1:
                PtzSetup1PmDefault.IsChecked = true;
                break;
            case 2:
                PtzSetup2PmDefault.IsChecked = true;
                break;
            case 3:
                PtzSetup3PmDefault.IsChecked = true;
                break;
            case 4:
                PtzSetup4PmDefault.IsChecked = true;
                break;
            default:
                PtzSetup1PmDefault.IsChecked = true;
                break;
        }
        switch (Config.User.PtzDefaultAM)
        {
            case 1:
                PtzSetup1AmDefault.IsChecked = true;
                break;
            case 2:
                PtzSetup2AmDefault.IsChecked = true;
                break;
            case 3:
                PtzSetup3AmDefault.IsChecked = true;
                break;
            case 4:
                PtzSetup4AmDefault.IsChecked = true;
                break;
            default:
                PtzSetup1AmDefault.IsChecked = true;
                break;
        }

        // Apply the options as well...
        _countdownViewer.SetWallpaperImage(Config.User.WallpaperFilespec);
        _countdownViewer.SetBaseFontSize(Config.User.FontSize);
        _countdownViewer.SetHorizontalMargin(Config.User.XPosition);
        _countdownViewer.SetBottomMargin(Config.User.YPosition);

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
        bool currentlyRunning = _appSettings.UseAtemConnection 
            && _atem is not null
            && _atem.IsCurrentlyRunning;

        if (_appSettings.UseAtemConnection && _atem is not null)
        {
            if (((runAction == RunAction.Toggle) && currentlyRunning) || (runAction == RunAction.ForceStop))
            {
                _atem.SendProgramToAux();
            }
            else if (((runAction == RunAction.Toggle) && !currentlyRunning) || (runAction == RunAction.ForceRun))
            {
                _atem.SendCountdownToAux();
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
        }
        StyleRunButton(currentlyRunning);

        // Note the current state for future.
        Config.User.CountdownShown = currentlyRunning;
    }

    /// <summary>
    /// Set the appearance of the Show/Hide Countdown button depending on the current operating mode
    /// </summary>
    private void StyleRunButton()
    {
        bool currentlyRunning = false;
        if (_appSettings.UseAtemConnection && _atem.AuxOutReady)
        {
            currentlyRunning = _atem.IsCurrentlyRunning;
        }
        StyleRunButton(currentlyRunning);
    }

    /// <summary>
    /// Set the appearance of the Show/Hide Countdown button and countdown status indicator depending on the current operating mode
    /// </summary>
    /// <param name="currentlyRunning">Use True if the countdown is being displayed currently, false otherwise</param>
    private void StyleRunButton(bool currentlyRunning)
    {
        if (_appSettings.UseAtemConnection && IsAtemConnected)
        {
            RunButton.IsEnabled = true;
            if (currentlyRunning)
            {
                CountdownIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.ReadyBackgroundBorderBrush);
                CountdownIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.ReadyMainBrush);
                CountdownLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
                CountdownLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
                CountdownLightText.Text = "Shown";
                RunButton.Content = "Hide";
            }
            else
            {
                CountdownIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.CautionBackgroundBorderBrush);
                CountdownIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.CautionMainBrush);
                CountdownLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CautionMainBrush);
                CountdownLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CautionMainBrush);
                CountdownLightText.Text = "Hidden";
                RunButton.Content = "Show";
            }
        }
        else
        {
            RunButton.IsEnabled = false;
            CountdownIndicatorBorder.Background = GetResource<SolidColorBrush>("Button.Disabled.Background");
            CountdownIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            CountdownLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            CountdownLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            CountdownLightText.Text = "Hidden";
            RunButton.Content = "Show";
        }
    }

    private void Heartbeat_Tick(object sender, EventArgs e)
    {
        // Check to see if a connection test is pending...
        if (DateTime.Compare(DateTime.Now, _pendingTestStart) >= 0)
        {
            // Start some tests
            PerformTests();
        }

        if (_appSettings.UseAtemConnection)
        {
            SetOnAirDisplay();
            SetAudioStatusDisplay();
        }

        if (_onAirTime > DateTime.MinValue)
        {
            // How long until the next service start?
            TimeSpan tMinus = TimeSpan.FromSeconds((_onAirTime - DateTime.Now).TotalSeconds + 1);

            if (tMinus.TotalHours > 100)
            {
                TimeToAirMessage.Text = ((int)tMinus.TotalHours).ToString() + "+ hrs";
            }
            else
            {
                TimeToAirMessage.Text = (tMinus.TotalHours >= 1 ? (((int)tMinus.TotalHours).ToString() + ":") : string.Empty)
                                      + tMinus.ToString("mm\\:ss");
            }

            // Show it on the viewer.
            _countdownViewer.SetCountdownTime(tMinus);

            TimeToAirIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.SubduedForegroundBrush);
            TimeToAirLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.SubduedForegroundBrush);

            // Set the colour according to the time remaining.
            if (DateTime.Compare(DateTime.Now, _onAirTime) == 1)
            {
                // When the clock runs down, stop showing the countdown.
                TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PlayingMainBrush);
                _countdownViewer.SetCountdownColour(CountdownViewer.CountdownColour.Red);

                ToggleRunningStatus(RunAction.ForceStop);
            }
            else if ((int)tMinus.TotalSeconds <= SecondsFromMinutesAndSecondsString(Config.User.TurnYellow))
            {
                TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CountdownWarningBrush);
                _countdownViewer.SetCountdownColour(CountdownViewer.CountdownColour.Yellow);
            }
            else if ((int)tMinus.TotalSeconds <= SecondsFromMinutesAndSecondsString(Config.User.TurnGreen))
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
        else
        {
            TimeToAirIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            TimeToAirLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            TimeToAirMessage.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            TimeToAirMessage.Text = "TBD";
        }

        ConsiderAutomaticActions();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // If we're showing the countdown, then take that down so as not to leave things hanging...
        ToggleRunningStatus(RunAction.ForceStop);

        // Save viewer window settings before closing...
        if (RestoreWindowLocationCheckbox.IsChecked?? false == true)
        {
            // Note the size, position and mode of the viewer window...
            Config.User.WindowLocation = new System.Drawing.Point
                (Convert.ToInt32(_countdownViewer.Left), Convert.ToInt32(_countdownViewer.Top));
            Config.User.WindowSize = new System.Drawing.Size
                (Convert.ToInt32(_countdownViewer.Width), Convert.ToInt32(_countdownViewer.Height));
            Config.User.FullScreenViewer = FullScreenViewerCheckBox.IsChecked ?? false;
        }
        Config.User.RestoreWindowLocation = RestoreWindowLocationCheckbox.IsChecked ?? false;
        Config.User.SuppressOnAirNotification = SuppressOnAirNotificationCheckbox.IsChecked ?? false;
        Config.User.Save();

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
        Config.User.AtemIpAddress = AtemIpAddressTextBox.Text.Trim();
        Config.User.Save();
        ConnectToAtem();
        ShowTestStatus();
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

        if (IsAtemConnected)
        {
            result = _atem.IsOnAir;
        }

        return result;
    }

    private void RestoreWindowLocationCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        Config.User.RestoreWindowLocation = true;
    }

    private void RestoreWindowLocationCheckbox_Unchecked(object sender, RoutedEventArgs e)
    {
        Config.User.RestoreWindowLocation = false;
    }

    private void SuppressOnAirNotificationCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        SetOnAirDisplay();
    }

    private void SuppressOnAirNotificationCheckbox_Unchecked(object sender, RoutedEventArgs e)
    {
        SetOnAirDisplay();
    }

    private async void RefreshWebPresenterConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        Config.User.WebPresenterIpAddress = WebPresenterIpAddressTextBox.Text.Trim();
        Config.User.Save();
        await ConnectToWebPresenter();
        ShowTestStatus();
    }

    private async void RefreshHyperDeckConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        Config.User.HyperDeckIpAddress = HyperDeckIpAddressTextBox.Text.Trim();
        Config.User.Save();
        await ConnectToHyperDeck();
        ShowTestStatus();
    }

    private void RefreshPtzCameraConnectionsButton_Click(object sender, RoutedEventArgs e)
    {
        Config.User.Camera1IpAddress = Camera1IpAddressTextBox.Text.Trim();
        Config.User.Camera2IpAddress = Camera2IpAddressTextBox.Text.Trim();
        Config.User.Save();
        ConnectToPtzCameras();
        ShowTestStatus();
    }

    private void RefreshEventScheduleApiButton_Click(object sender, RoutedEventArgs e)
    {
        DetermineNextStartTime();
        ShowTestStatus();
    }

    private void TestStatusButton_Click(object sender, RoutedEventArgs e)
    {
        // Give the app a couple of seconds to paint and then do a full round of connection tests
        _pendingTestStart = DateTime.Now.AddSeconds(_appSettings.SecondsToWaitForTesting);

        ShowTestStatus();
    }

    private void StatusIndicatorBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_inCautionStatus)
        {
            // See https://stackoverflow.com/questions/7929646/how-to-programmatically-select-a-tabitem-in-wpf-tabcontrol
            // for why we have to do this arcane thing...
            Dispatcher.BeginInvoke((Action)(() => TabList.SelectedIndex = 1));
        }
        else
        {
            TestStatusButton_Click(this, e);
        }
    }

    private void ModeIndicatorBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        GoLiveModeButton_Click(this, e);
    }

    private void CountdownIndicatorBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        RunButton_Click(this, e);
    }

    private void GoLiveModeButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle the Go Live Auto/Manual mode
        Config.User.AutomaticMode = !Config.User.AutomaticMode;
        SetGoLiveModeIndicatorDisplay();
    }

    private void PtzPreset1SetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySpecificPtzSetup(1);
    }

    private void PtzPreset2SetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySpecificPtzSetup(2);
    }

    private void PtzPreset3SetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySpecificPtzSetup(3);
    }

    private void PtzPreset4SetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySpecificPtzSetup(4);
    }

    private void PtzSetup1NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string newValue = PtzSetup1NameTextBox.Text.Trim();
        SetPtzSetup1ButtonText.Text = newValue;
        Config.User.PtzSetup1Name = newValue;
    }

    private void PtzSetup2NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string newValue = PtzSetup2NameTextBox.Text.Trim();
        SetPtzSetup2ButtonText.Text = newValue;
        Config.User.PtzSetup2Name = newValue;
    }

    private void PtzSetup3NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string newValue = PtzSetup3NameTextBox.Text.Trim();
        SetPtzSetup3ButtonText.Text = newValue;
        Config.User.PtzSetup3Name = newValue;
    }

    private void PtzSetup4NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string newValue = PtzSetup4NameTextBox.Text.Trim();
        SetPtzSetup4ButtonText.Text = newValue;
        Config.User.PtzSetup4Name = newValue;
    }

    /// <summary>
    /// Performs a full suite of connection tests / (re) establishes all appropriate connections
    /// </summary>
    private async void PerformTests()
    {
        if (!_activelyTesting)
        {
            _activelyTesting = true;    // Flag to prevent re-entrancy.

            // Figure out the next on-air time...
            DetermineNextStartTime();

            // Connect to the ATEM switcher...
            ConnectToAtem();

            // Connect to the HyperDeck recorder...
            await ConnectToHyperDeck();

            // Connect to the WebPresenter streaming deck...
            await ConnectToWebPresenter();

            // Connect to the PTZ Cameras...
            ConnectToPtzCameras();

            // When tests are complete reset the test sentinels
            _lastTestFinish = DateTime.Now;
            _pendingTestStart = DateTime.MaxValue;

            // Show the immediate results of the tests.
            ShowTestStatus();

            // Apply the starting PTZ Setup (if and when applicable)
            ApplyDefaultPtzSetup();

            _activelyTesting = false;
        }
    }

    private void ShowTestStatus()
    {
        _inCautionStatus = false;
        // What is the test status?
        if (_pendingTestStart == DateTime.MaxValue && _lastTestFinish == DateTime.MinValue)
        {
            // No tests have yet been performed or scheduled
            StatusIndicatorBorder.Background = GetResource<SolidColorBrush>("Button.Disabled.Background");
            StatusIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            StatusLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            StatusLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            StatusLightText.Text = "TBD";
            TestStatusButton.IsEnabled = true;
        }
        else 
        {
            if (_pendingTestStart == DateTime.MaxValue)
            {
                // No new tests are scheduled. Status is as per current indicators
                // Work out what the status indication should be
                bool ready = true;  // Until we find out otherwise.

                // Check the ATEM connection if applicable and we're not wasting
                // time because we're already in caution state
                if (ready && _appSettings.UseAtemConnection)
                {
                    ready = ready && IsAtemConnected;
                    if (ready)
                    {
                        ready &= _atem.CountdownInputReady
                              && _atem.InputTitleCardReady
                              && _atem.Input1Ready
                              && _atem.Input2Ready
                              // The Mooseware lab doesn't have an ATEM with Aux Out so ignore it
                              // when when the feature switch is off.
                              && (!_appSettings.UseAuxOut || _atem.AuxOutReady)
                              && _atem.PvwOutReady
                              && _atem.PgmOutReady;
                    }
                }

                // Check the HyperDeck connection if applicable and we're not
                // wasting time because we're already in a caution state
                if (ready && _appSettings.UseHyperDeckConnection)
                {
                    ready &= IsHyperDeckConnected
                          && RecordingMinutesRemaining > _appSettings.RecordingMinutesYellow;
                }

                // Check the WebPresenter connection if applicable and we're not
                // wasting time because we're already in a caution state
                if (ready && _appSettings.UseWebPresenterConnection)
                {
                    ready &= IsWebPresenterConnected;
                }

                // Check the PTZ Camera connections if applicable and we're not
                // wasting time because we're already in a caution state
                if (ready && _appSettings.UsePtzCameraConnections)
                {
                    ready &= IsCam1Connected
                          && IsCam2Connected;
                }

                // TODO: Factor the remaining connection statuses into the ShowTestStatus() result;
                // Basically this is just the YouTube API at this point

                if (ready && _appSettings.UseScheduleApiConnection)
                {
                    ready &= (DateTime.Compare(_onAirTime, DateTime.Now) > 0);
                }

                // Now show the end result of the assessment...
                if (ready)
                {
                    StatusIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.ReadyBackgroundBorderBrush);
                    StatusIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.ReadyMainBrush);
                    StatusLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
                    StatusLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
                    StatusLightText.Text = "Ready";
                }
                else
                {
                    StatusIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.CautionBackgroundBorderBrush);
                    StatusIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.CautionMainBrush);
                    StatusLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CautionMainBrush);
                    StatusLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.CautionMainBrush);
                    StatusLightText.Text = "Caution";
                    _inCautionStatus = true;
                }
                TestStatusButton.IsEnabled = true;
            }
            else
            {
                // Tests are currently active or pending
                StatusIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.PendingBackgroundBorderBrush);
                StatusIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.PendingForegroundBrush);
                StatusLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PendingForegroundBrush);
                StatusLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PendingForegroundBrush);
                StatusLightText.Text = "Testing";
                TestStatusButton.IsEnabled = false;
            }
        }
    }

    private void SetGoLiveModeIndicatorDisplay()
    {
        if (Config.User.AutomaticMode == true)
        {
            ModeIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.ReadyBackgroundBorderBrush);
            ModeIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.ReadyMainBrush);
            ModeLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            ModeLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            ModeLightText.Text = "Auto";
        }
        else
        {
            ModeIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.ManualModeBackgroundBorderBrush);
            ModeIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.ManualModeBorderBrush);
            ModeLightCaptionText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.ManualModeMainBrush);
            ModeLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.ManualModeMainBrush);
            ModeLightText.Text = "Manual";
        }
        _countdownViewer.SetManualModeIndicator(!Config.User.AutomaticMode);
        GoLiveModeButton.IsEnabled = true;
    }

    /// <summary>
    /// Sets the visual properties of the program audio indicator
    /// </summary>
    internal void SetAudioStatusDisplay()
    {
        if (_appSettings.UseAtemConnection && IsAtemConnected)
        {
            double currentPgmGain = _atem.ProgramGain;

            // Is the volume more or less full?
            if (Math.Abs(currentPgmGain - _appSettings.VolumeFullDb) < 1.0
                || currentPgmGain > _appSettings.VolumeFullDb)
            {
                SoundIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.ReadyBackgroundBorderBrush);
                SoundIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.ReadyMainBrush);
                SoundLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
                SoundLightText.Text = "Full";
            }
            // Is the volume more or less off?
            else if (Math.Abs(currentPgmGain - _appSettings.VolumeOffDb) < 1.0
                || currentPgmGain < _appSettings.VolumeOffDb)
            {
                SoundIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.MuteModeBackgroundBorderBrush);
                SoundIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.MuteModeBorderBrush);
                SoundLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.MuteModeMainBrush);
                SoundLightText.Text = "Mute";
            }
            else
            {
                // The volume is in transition
                SoundIndicatorBorder.Background = AppResources.DefinedColour(AppResources.StaticResource.PendingBackgroundBorderBrush);
                SoundIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.PendingForegroundBrush);
                SoundLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.PendingForegroundBrush);
                SoundLightText.Text = currentPgmGain.ToString("0") + "dB";
            }
        }
        else
        {
            SoundIndicatorBorder.Background = GetResource<SolidColorBrush>("Button.Disabled.Background");
            SoundIndicatorBorder.BorderBrush = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            SoundLightText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.DisabledMainBrush);
            SoundLightText.Text = "Mute";
        }
    }

    /// <summary>
    /// Gets an application WPF resource at runtime using it's key
    /// Useful for getting brushes from control templates, etc.
    /// </summary>
    /// <typeparam name="T">The type of resource to be retrieved</typeparam>
    /// <param name="resourceName">The key of the resource to be found</param>
    /// <returns></returns>
    private static T GetResource<T>(string resourceName) where T : class
    {
        return Application.Current.TryFindResource(resourceName) as T;
    }

    private void PtzSetupPmDefault_Checked(object sender, RoutedEventArgs e)
    {
        if (e.Source is RadioButton option)
        {
            string selectedOptionName = option.Name;
            switch (selectedOptionName)
            {
                case "PtzSetup1PmDefault":
                    Config.User.PtzDefaultPM = 1;
                    break;
                case "PtzSetup2PmDefault":
                    Config.User.PtzDefaultPM = 2;
                    break;
                case "PtzSetup3PmDefault":
                    Config.User.PtzDefaultPM = 3;
                    break;
                case "PtzSetup4PmDefault":
                    Config.User.PtzDefaultPM = 4;
                    break;
                default:
                    break;
            }
        }
    }

    private void PtzSetupAmDefault_Checked(object sender, RoutedEventArgs e)
    {
        if (e.Source is RadioButton option)
        {
            string selectedOptionName = option.Name;
            switch (selectedOptionName)
            {
                case "PtzSetup1AmDefault":
                    Config.User.PtzDefaultAM = 1;
                    break;
                case "PtzSetup2AmDefault":
                    Config.User.PtzDefaultAM = 2;
                    break;
                case "PtzSetup3AmDefault":
                    Config.User.PtzDefaultAM = 3;
                    break;
                case "PtzSetup4AmDefault":
                    Config.User.PtzDefaultAM = 4;
                    break;
                default:
                    break;
            }
        }
    }

    private void SoundIndicatorBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_appSettings.UseAtemConnection && IsAtemConnected && _atem is not null)
        {
            // Toggle the volume between on and off
            double atemGain = _atem.ProgramGain;
            if (atemGain < _appSettings.VolumeOffDb
                || Math.Abs(atemGain - _appSettings.VolumeOffDb) < 1)
            {
                _atem.ProgramGain = _appSettings.VolumeFullDb;
            }
            else
            {
                _atem.ProgramGain = _appSettings.VolumeOffDb;
            }
            SetAudioStatusDisplay();
        }

    }

    private void ApplyDefaultPtzSetup()
    {
        if (_ptzSetupApplied) return;

        if (_appSettings.UseAtemConnection 
            && IsAtemConnected
            )
        {
            _atem.SendTitleCardToProgram();
            _atem.ProgramGain = _appSettings.VolumeOffDb;
        }

        if (_appSettings.UsePtzCameraConnections
         && IsCam1Connected && IsCam2Connected)
        {
            PtzPreset1SetButton.IsEnabled = true;
            PtzPreset2SetButton.IsEnabled = true;
            PtzPreset3SetButton.IsEnabled = true;
            PtzPreset4SetButton.IsEnabled = true;

            SetPtzSetup1ButtonText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            SetPtzSetup2ButtonText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            SetPtzSetup3ButtonText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            SetPtzSetup4ButtonText.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);

            PtzPresetCamera1Label.Foreground = AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
            PtzPresetCamera2Label.Foreground = AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
            PtzPresetCamera1Preset.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            PtzPresetCamera2Preset.Foreground = AppResources.DefinedColour(AppResources.StaticResource.BrightForegroundBrush);
            
            int setupNumber;
            // Apply the default setup according to the time of day of the stream start date/time.
            if (_onAirTime.Hour <= 12)
            {
                // Use the AM default
                setupNumber = Config.User.PtzDefaultAM;
            }
            else
            {
                // Use the PM default
                setupNumber = Config.User.PtzDefaultPM;
            }
            ApplySpecificPtzSetup(setupNumber);
        }

        _ptzSetupApplied = true;
    }

    private void ApplySpecificPtzSetup(int setupNumber)
    {
        int cam1Preset = 0;
        int cam2Preset = 0;
        int previewCamera = 0;
        // Get the PTZ presets for the selected setup
        switch (setupNumber)
        {
            case 1:
                cam1Preset = Config.User.PtzSetup1Cam1;
                cam2Preset = Config.User.PtzSetup1Cam2;
                if ((bool)PtzSetup1Cam1.IsChecked)
                {
                    previewCamera = 1;
                }
                else if ((bool)PtzSetup1Cam2.IsChecked)
                {
                    previewCamera = 2;
                }
                break;
            case 2:
                cam1Preset = Config.User.PtzSetup2Cam1;
                cam2Preset = Config.User.PtzSetup2Cam2;
                if ((bool)PtzSetup2Cam1.IsChecked)
                {
                    previewCamera = 1;
                }
                else if ((bool)PtzSetup2Cam2.IsChecked)
                {
                    previewCamera = 2;
                }
                break;
            case 3:
                cam1Preset = Config.User.PtzSetup3Cam1;
                cam2Preset = Config.User.PtzSetup3Cam2;
                if ((bool)PtzSetup3Cam1.IsChecked)
                {
                    previewCamera = 1;
                }
                else if ((bool)PtzSetup3Cam2.IsChecked)
                {
                    previewCamera = 2;
                }
                break;
            case 4:
                cam1Preset = Config.User.PtzSetup4Cam1;
                cam2Preset = Config.User.PtzSetup4Cam2;
                if ((bool)PtzSetup4Cam1.IsChecked)
                {
                    previewCamera = 1;
                }
                else if ((bool)PtzSetup4Cam2.IsChecked)
                {
                    previewCamera = 2;
                }
                break;
            default:
                break;
        }
        PtzPreset1SetButton.BorderBrush = setupNumber == 1 ?
            AppResources.DefinedColour(AppResources.StaticResource.SelectedPresetBorderBrush)
            : AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
        PtzPreset2SetButton.BorderBrush = setupNumber == 2 ?
            AppResources.DefinedColour(AppResources.StaticResource.SelectedPresetBorderBrush)
            : AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
        PtzPreset3SetButton.BorderBrush = setupNumber == 3 ?
            AppResources.DefinedColour(AppResources.StaticResource.SelectedPresetBorderBrush)
            : AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
        PtzPreset4SetButton.BorderBrush = setupNumber == 4 ?
            AppResources.DefinedColour(AppResources.StaticResource.SelectedPresetBorderBrush)
            : AppResources.DefinedColour(AppResources.StaticResource.LightForegroundBrush);
        // Apply the settings...
        PtzCameraController ptzCam;
        if (IsCam1Connected)
        {
            ptzCam = new(Config.User.Camera1IpAddress);
            ptzCam.RecallPreset(cam1Preset);
            ptzCam = null;
            PtzPresetCamera1Preset.Text = "Preset " + cam1Preset.ToString();
        }
        if (IsCam1Connected)
        {
            ptzCam = new(Config.User.Camera2IpAddress);
            ptzCam.RecallPreset(cam2Preset);
            ptzCam = null;
            PtzPresetCamera2Preset.Text = "Preset " + cam2Preset.ToString();
        }
        if (_appSettings.UseAtemConnection)
        {
            if (IsAtemConnected && _atem is not null)
            {
                if (previewCamera == 1)
                {
                    _atem.PreviewCamera1();
                    _secondaryCamera = 2;
                } else if (previewCamera == 2)
                {
                    _atem.PreviewCamera2();
                    _secondaryCamera = 1;
                }
            }
        }
    }

    /// <summary>
    /// Do any automatic actions that are called for based on the current clock and configuration
    /// </summary>
    private void ConsiderAutomaticActions()
    {
        DateTime now = DateTime.Now;

        // Do automatic countdown actions if we're in auto mode
        if (Config.User.AutomaticMode)
        {
            if (now < _streamStartTime && !_startedStreaming)
            {
                // Time to start streaming
                if (_appSettings.UseWebPresenterConnection
                    && IsWebPresenterConnected
                    && _webPresenter is not null)
                {
                    _webPresenter.StartLivestream().Wait();
                }
                _startedStreaming = true;
            }

            if (now < _startRecordingTime && !_startedRecording)
            {
                // Time to start recording
                if (_appSettings.UseHyperDeckConnection
                    && IsHyperDeckConnected
                    && _hyperDeck is not null)
                {
                    // Create a clip title based on the user-supplied template...
                    string clipTitle = Config.User.LocalRecordingName;
                    if (string.IsNullOrEmpty(clipTitle))
                    {
                        clipTitle = "SolelLiveStream_" + _onAirTime.ToString("yyyy-MM-dd_HHmm");
                    }
                    else
                    {
                        // Replace any tokens in the string
                        if (clipTitle.Contains("[title]"))
                        {
                            string fullTitle = _nextEventTitle;
                            if (!string.IsNullOrEmpty(_nextEventSubtitle))
                            {
                                fullTitle += "-" + _nextEventSubtitle;
                            }
                            clipTitle = clipTitle.Replace("[title]", fullTitle);
                        }
                        if (clipTitle.Contains("[yyyymmdd]"))
                        {
                            clipTitle = clipTitle.Replace("[yyyymmdd]", _onAirTime.ToString("yyyy-MM-dd"));
                        }
                        if (clipTitle.Contains("[hhmm]"))
                        {
                            clipTitle = clipTitle.Replace("[hhmm]", _onAirTime.ToString("HHmm"));
                        }
                    }
                    _hyperDeck.StartRecording(clipTitle);
                }
                _startedRecording = true;
            }

            if (now >= _onAirTime && !_goneOnAir)
            {
                // TODO: (TESTING RQD!) Time to go on air automatically

                // Fade the audio up to full volume
                _atem.FadeProgramAudio(_appSettings.VolumeFullDb, 2.0, this);

                // Perform an AUTO transition
                _atem.PerformAutoTransition();

                _goneOnAir = true;
            }

            if (now >= _secondaryCameraPreviewTime && !_previewedSecondaryCamera)
            {
                // Time to select the secondary camera
                if (_appSettings.UseAtemConnection
                    && IsAtemConnected
                    && _atem is not null)
                {
                    if (_secondaryCamera == 1)
                    {
                        _atem.PreviewCamera1();
                    }
                    else if (_secondaryCamera == 2)
                    {
                        _atem.PreviewCamera2();
                    }
                }
            }
        }

        // Do the auto shutdown(if user config calls for it)
        if (Config.User.ShutDownAfterGoLive)
        {
            if (now >= _autoShutDownTime)
            {
                this.Close();
            }
        }
    }

    private void AutoShutdownCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        Config.User.ShutDownAfterGoLive = true;
        CalculateAutomaticShutdownTime();
    }

    private void AutoShutdownCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        Config.User.ShutDownAfterGoLive = false;
        CalculateAutomaticShutdownTime();
    }

    private void CalculateAutomaticShutdownTime()
    {
        if (Config.User.ShutDownAfterGoLive && _onAirTime > DateTime.Now)
        {
            _autoShutDownTime = _onAirTime.AddSeconds(_appSettings.AutoShutdownTimeout);
        }
        else
        {
            _autoShutDownTime = DateTime.MaxValue;
        }
    }

    private void RecordingFileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Config.User.LocalRecordingName = RecordingFileNameTextBox.Text.Trim();
    }
}


