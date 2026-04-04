using Microsoft.Extensions.Logging;
using Mooseware.Tachnit.AtemApi;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Mooseware.TimeToAir;

public class Switcher
{
    /// <summary>
    /// IpAddress of the (connected) ATEM switcher
    /// </summary>
    public string IpAddress { get => _atem.IpAddress; }
    /// <summary>
    /// Whether or not a connection to the ATEM switcher was successfully established
    /// </summary>
    public bool IsReady { get => _atem.IsReady; }
    /// <summary>
    /// The description of the problem (when applicable) attempting to connect to the ATEM switcher
    /// </summary>
    public string NotReadyReason { get => _atem.NotReadyReason; }
    /// <summary>
    /// The Camera 1 input
    /// </summary>
    public Input Input1 { get; private set; }
    /// <summary>
    /// Whether or not the ATEM input for camera 1 exists on the connected ATEM switcher
    /// </summary>
    public bool Input1Ready { get; private set; }
    /// <summary>
    /// The Camera 2 input
    /// </summary>
    public Input Input2 { get; private set; }
    /// <summary>
    /// Whether or not the ATEM input for camera 2 exists on the connected ATEM switcher
    /// </summary>
    public bool Input2Ready { get; private set; }
    /// <summary>
    /// The Media Player 1 input
    /// </summary>
    public Input InputTitleCard { get; private set; }
    /// <summary>
    /// Whether or not the ATEM input for Media Player 1 exists on the connected ATEM switcher
    /// </summary>
    public bool InputTitleCardReady { get; private set; }
    /// <summary>
    /// The Input where the countdown window will be shown
    /// </summary>
    public Input CountdownInput { get; private set; }
    /// <summary>
    /// Whether or not the ATEM input on which the countdown is meant to be shown exists on the connected ATEM switcher
    /// </summary>
    public bool CountdownInputReady { get; private set; }
    /// <summary>
    /// The Auxiliary output port of the ATEM
    /// </summary>
    public Input AuxOutput { get; private set; }
    /// <summary>
    /// Whether or not the Aux output port exists on the connected ATEM switcher
    /// </summary>
    public bool AuxOutReady { get; private set; }
    /// <summary>
    /// The Preview feed port of the ATEM
    /// </summary>
    public Input PvwOutput { get; private set; }
    /// <summary>
    /// Whether or not the Preview output port exists on the connected ATEM switcher
    /// </summary>
    public bool PvwOutReady { get; private set; }
    /// <summary>
    /// The Program feed port of the ATEM
    /// </summary>
    public Input PgmOutput { get; private set; }
    /// <summary>
    /// Whether or not the Program output port exists on the connected ATEM switcher
    /// </summary>
    public bool PgmOutReady { get; private set; }
    /// <summary>
    /// Internal reference to the ATEM switcher control interface
    /// </summary>
    private readonly AtemSwitcher _atem;
    /// <summary>
    /// Timer for ticking through a loop to look for audio fading up/down
    /// </summary>
    private readonly DispatcherTimer _faderHeartbeat;
    /// <summary>
    /// For an audio fading operation, the starting point (volume) for the fade
    /// </summary>
    private double _audioFadeStart = 0.0;
    /// <summary>
    /// For an audio fading operation, the desired endpoint (volume) for the fade
    /// </summary>
    private double _audioFadeFinish = 0.0;
    /// <summary>
    /// The total duration of the fade operation in milliseconds
    /// </summary>
    private int _audioFadeProgressTarget = 0;
    /// <summary>
    /// The number of milliseconds since the start of the fade operation
    /// </summary>
    private int _audioFadeProgress = 0;
    /// <summary>
    /// Logging instance established at the application start
    /// </summary>
    private readonly ILogger<MainWindow> _logger;

    /// <summary>
    /// Reference to the MainWindow so that status indications (e.g. during fade operations) can be communicated to the UI
    /// </summary>
    private MainWindow _audioStatusIndicatorCallback;

    public Switcher(ILogger<MainWindow> logger, string atemIpAddress = "192.168.1.240", string input1 = "CAM1", string input2 = "CAM2",
        string inputTitleCard = "MP1", string inputCountdown = "PC4")
    {
        _logger = logger;

        // Connect to the _switcher, if it's where it should be...
        _atem = new(atemIpAddress);

        Input1 = null;              // All until proven otherwise.
        Input2 = null;
        CountdownInput = null;
        InputTitleCard = null;
        AuxOutput = null;
        PvwOutput = null;
        PgmOutput = null;

        Input1Ready = false;
        Input2Ready = false;
        CountdownInputReady = false;
        InputTitleCardReady = false;
        AuxOutReady = false;
        PvwOutReady = false;
        PgmOutReady = false;

        if (_atem.IsReady)
        {
            // Do we have the first input?
            if (_atem.HasInput(input1))
            {
                Input1 = _atem.GetInputByName(input1);
                Input1Ready = (Input1 is not null);
            }
            // Do we have the second input?
            if (_atem.HasInput(input2))
            {
                Input2 = _atem.GetInputByName(input2);
                Input2Ready = (Input2 is not null);
            }
            // Do we have the MP1 input?
            if (_atem.HasInput(inputTitleCard))
            {
                InputTitleCard = _atem.GetInputByName(inputTitleCard);
                InputTitleCardReady = (InputTitleCard is not null);
            }
            // Do we have the Countdown input?
            if (_atem.HasInput(inputCountdown))
            {
                CountdownInput = _atem.GetInputByName(inputCountdown);
                CountdownInputReady = (CountdownInput is not null);
            }
            // Do we have the Aux Output?
            if (_atem.HasInput("AUX1"))
            {
                AuxOutput = _atem.GetInputByName("AUX1");
                AuxOutReady = (AuxOutput is not null);
            }
            // Do we have the Preview port?
            if (_atem.HasInput("PVW"))
            {
                PvwOutput = _atem.GetInputByName("PVW");
                PvwOutReady = (PvwOutput is not null);
            }
            // Do we have the Program port?
            {
                PgmOutput = _atem.GetInputByName("PGM");
                PgmOutReady = (PgmOutput is not null);
            }
        }

        // Set up the timer that handles audio fading in and out
        _faderHeartbeat = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        _faderHeartbeat.Tick += FaderHeartbeat_Tick;

    }

    /// <summary>
    /// Preview the title card feed and then CUT to program
    /// </summary>
    public void SendTitleCardToProgram()
    {
        _logger.LogInformation("SendTitleCardToProgram()");
        if (IsReady && InputTitleCardReady && PvwOutReady)
        {
            _atem.SetPreviewInput(InputTitleCard.InputID);
            _atem.PerformCut();
        }
    }

    /// <summary>
    /// Send the Input 1 feed to Preview
    /// </summary>
    public void PreviewCamera1()
    {
        _logger.LogInformation("PreviewCamera1()");
        if (IsReady && Input1Ready && PvwOutReady)
        {
            _atem.SetPreviewInput(Input1.InputID);
        }
    }

    /// <summary>
    /// Send the Input 2 feed to Preview
    /// </summary>
    public void PreviewCamera2()
    {
        _logger.LogInformation("PreviewCamera2()");
        if (IsReady && Input1Ready && PvwOutReady)
        {
            _atem.SetPreviewInput(Input2.InputID);
        }
    }

    /// <summary>
    /// Send the Program feed to the Aux Out port
    /// </summary>
    public void SendProgramToAux()
    {
        _logger.LogInformation("SendProgramToAux()");
        if (IsReady && AuxOutReady && PgmOutReady)
        {
            _atem.SetAuxInput(PgmOutput);
        }
    }

    /// <summary>
    /// Send the Countdown Input feed to the Aux Out port
    /// </summary>
    public void SendCountdownToAux()
    {
        _logger.LogInformation("SendCountdownToAux()");
        if (IsReady && AuxOutReady && CountdownInputReady)
        {
            _atem.SetAuxInput(CountdownInput);
        }
    }

    /// <summary>
    /// Perform an AUTO transition in the mix-effect block, fading the preview to program (and vice versa)
    /// </summary>
    public void PerformAutoTransition()
    {
        _logger.LogInformation("PerformAutoTransition()");
        if (IsReady)
        {
            _atem.PerformAuto();
        }
    }

    /// <summary>
    /// Returns True if the current Auxilliary output port's source is the Countdown input feed or false otherwise
    /// </summary>
    public bool IsCurrentlyRunning
    {
        get
        {
            bool result = false;

            if (IsReady && AuxOutReady && CountdownInputReady)
            {
                Input auxSource = _atem.GetAuxInput();
                if (auxSource is not null)
                {
                    result = (auxSource.InputID == CountdownInput.InputID);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Returns True if the current program source is either Camera 1 or Camera 2
    /// </summary>
    public bool IsOnAir
    {
        get
        {
            bool result = false;

            Input pgmSource = _atem.GetProgramInput();
            if (pgmSource is not null)
            {
                result = ((Input1Ready && pgmSource.InputID == Input1.InputID)
                    || (Input2Ready && pgmSource.InputID == Input2.InputID));
            }

            return result;
        }
    }

    /// <summary>
    /// Returns the current volume level for the Program channel.
    /// If the switcher is not connected, assume unity gain (0.0dB)
    /// </summary>
    public double ProgramGain
    {
        get
        {
            double result = 0;

            if (_atem is not null)
            {
                result = _atem.GetProgramVolume();
            }

            return result;
        }
        set
        {
            _logger.LogTrace("Set ProgramGain({value})", value);
            _atem?.SetProgramVolume(value);
        }
    }

    /// <summary>
    /// Get a list of short names actually found on the ATEM switcher
    /// </summary>
    public List<string> SwitcherInputShortNames
    {
        get
        {
            return _atem.GetInputShortNames();
        }
    }

    /// <summary>
    /// Get a listing of general properties for the inputs found on the ATEM switcher
    /// </summary>
    public List<string> ListSwitcherInputs
    {
        get
        {
            List<string> results = [];

            foreach (var input in _atem.SwitcherInputs)
            {
                if (input.ShortName != null && input.ShortName.Length > 0)
                {
                    results.Add($"Short name=[{input.ShortName}] InputId=[{input.InputID}] "
                        + $"PortType=[{input.PortType}] ({((long)input.PortType)})");
                }
            }

            return results;
        }
    }

    public void FadeProgramAudio(double targetVolume, double fadeDuration, MainWindow audioStatusIndicatorCallback)
    {
        _logger.LogTrace("FadeProgramAudio()");
        _audioStatusIndicatorCallback = audioStatusIndicatorCallback;

        if (_atem.IsReady && !_faderHeartbeat.IsEnabled)
        {
            // What volume are we starting at?
            _audioFadeStart = _atem.GetProgramVolume();

            // Where are we going?
            _audioFadeFinish = Math.Min(Math.Max(-60.0, targetVolume), 10.0);

            // How long are we going to take to get there?
            _audioFadeProgressTarget = (int)((1000 * fadeDuration) / _faderHeartbeat.Interval.TotalMilliseconds);

            _audioFadeProgress = 0;

            _logger.LogInformation("FadeProgramAudio(): Start={start} Finish={finish} Duration={duration}",
                _audioFadeStart, _audioFadeFinish, fadeDuration);

            _faderHeartbeat.Start();
        }
    }

    private void FaderHeartbeat_Tick(object sender, EventArgs e)
    {
        _audioFadeProgress += (int)(_faderHeartbeat.Interval.TotalMilliseconds);
        double newVolume = _audioFadeFinish;    // End result by default
        if (_audioFadeProgress <= _audioFadeProgressTarget)
        {
            // How far along are we in terms of the desired time scale (as a factor from 0.0 to 1.0)...
            double progress = (double)_audioFadeProgress / (double)_audioFadeProgressTarget;

            // Calculate the volume value to set based on the progress using a cubic ease in and out function...
            double newVolumeFactor = progress < 0.5 ? 4 * Math.Pow(progress, 3) : 1 - Math.Pow(-2 * progress + 2, 3) / 2;
            newVolume = ((_audioFadeFinish - _audioFadeStart) * newVolumeFactor) + _audioFadeStart;
        }
        else
        {
            // We're done fading
            _faderHeartbeat.Stop();
        }
        _logger.LogTrace("FaderHeartbeat_Tick(): SetProgramVolume({value})", newVolume);
        _atem.SetProgramVolume(newVolume);

        // Update the GUI with the current volume
        _audioStatusIndicatorCallback.SetAudioStatusDisplay();
    }
}
