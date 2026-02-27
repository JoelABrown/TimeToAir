namespace Mooseware.TimeToAir.Configuration;

/// <summary>
/// Application configuration settings (read-only at application start) for configuring the application
/// The settings are loaded from appsettings.json in a section called "ApplicationSettings".
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The number of seconds after going live that the application shuts down automatically.
    /// Use a value <= 0 to turn off this feature.
    /// </summary>
    public int AutoShutdownTimeout { get; set; } = 30;

    /// <summary>
    /// The lowest end of the legal range for tempo as an integer percentage (% * 100) of original
    /// </summary>
    public double VolumeOffDb { get; set; } = -60.0;

    /// <summary>
    /// The highest end of the legal range for tempo as an integer percentage (% * 100) of original
    /// </summary>
    public double VolumeFullDb { get; set; } = 0.0;

    /// <summary>
    /// The number of seconds over which to fade the volume at go on air time
    /// </summary>
    public double VolumeFadeDuration { get; set; } = 1.0;

    /// <summary>
    /// The URL of the website where the date and time of the next livestream event is provided
    /// </summary>
    public string EventScheduleUrl { get; set; } = string.Empty;

    /// <summary>
    /// The URL of the YouTube API
    /// </summary>
    public string YouTubeApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// The number of seconds after startup to wait before performing an initial connection test.
    /// </summary>
    public int SecondsToWaitForTesting { get; set; } = 1;

    /// <summary>
    /// The number of seconds to force the countdown start for testing purposes.
    /// Use a value greater than zero to set the forced On Air Time at startup
    /// Any value <= 0 ignores this setting and the normal On Air Time determination
    /// process is used.
    /// </summary>
    public int ForceTestCountdownSeconds { get; set; } = 0;

    /// <summary>
    /// The number of minutes of recording capacity below which the status indicator is Yellow
    /// </summary>
    public int RecordingMinutesYellow { get; set; } = 0;

    /// <summary>
    /// The number of minutes of recording capacity below which the status indicator is Red
    /// </summary>
    public int RecordingMinutesRed { get; set; } = 0;

    /// <summary>
    /// The number of seconds after going live (automatically) that the secondary camera
    /// is sent to the Preview input.
    /// </summary>
    public int SecondaryCameraPreviewDelay { get; set; } = 10;

    // ----------------------------------------------------------------------
    // FEATURE FLAGS
    // ----------------------------------------------------------------------

    /// <summary>
    /// Whether or not to make calls to the ATEM video switcher
    /// </summary>
    public bool UseAtemConnection { get; set; } = false;

    /// <summary>
    /// Whether or not to use or ignore the AUX OUT connection of the video switcher
    /// </summary>
    public bool UseAuxOut { get; set; } = false;

    /// <summary>
    /// Whether or not to make calls to the WebPresenter streaming bridge
    /// </summary>
    public bool UseWebPresenterConnection { get; set; } = false;

    /// <summary>
    /// Whether or not to make calls to the HyperDeck recording deck
    /// </summary>
    public bool UseHyperDeckConnection { get; set; } = false;

    /// <summary>
    /// Whether or not to make calls to the PTZ Cameras
    /// </summary>
    public bool UsePtzCameraConnections { get; set; } = false;

    /// <summary>
    /// Whether or not to make calls to the YouTube API for live streaming event scheduling
    /// </summary>
    public bool UseYouTubeConnection { get; set; } = false;

    /// <summary>
    /// Whether or not to make calls to the Schedule API website for getting the next event date/time
    /// </summary>
    public bool UseScheduleApiConnection { get; set; } = false;

}
