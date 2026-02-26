using BMDSwitcherAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mooseware.Tachnit.AtemApi;

/// <summary>
/// A wrapper on selected functions from the BMD ATEM Switcher API for use in C# projects
/// </summary>
public class AtemSwitcher
{
    /// <summary>
    /// Reference to the ATEM Switcher API
    /// </summary>
    private readonly IBMDSwitcher _switcher;

    /// <summary>
    /// Descriptive reason for why the attempt to connect to the ATEM switcher failed, when applicable
    /// </summary>
    public string NotReadyReason { get; private set; }

    /// <summary>
    /// Whether or not the connection to an ATEM switcher has been successfully established
    /// </summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// The IP Address at which the ATEM switcher was discovered
    /// </summary>
    public string IpAddress { get; private set; }

    /// <summary>
    /// Internal refernece to the first Mix Effect Block on the switcher (TV Studio HD only has 1 ME block)
    /// </summary>
    private readonly IBMDSwitcherMixEffectBlock _me0;

    /// <summary>
    /// Internal reference to the first Auxillary Output of the switcher (TV Studio HD only has 1 Aux Out)
    /// </summary>
    private readonly IBMDSwitcherInputAux _auxOut;

    /// <summary>
    /// Internal reference to the Program feed of the ATEM switcher
    /// </summary>
    private readonly IBMDSwitcherInput _pgmOut;

    /// <summary>
    /// Internal reference to the Preview feed of the ATEM switcher
    /// </summary>
    private readonly IBMDSwitcherInput _pvwOut;

    /// <summary>
    /// Internal reference to the classic audio mixer in the ATEM switcher
    /// </summary>
    private readonly IBMDSwitcherAudioMixer _audioMixer;

    /// <summary>
    /// Internal reference to the Fairlight audio mixer in the ATEM switcher
    /// </summary>
    private readonly IBMDSwitcherFairlightAudioMixer _fairlightMixer;

    /// <summary>
    /// List of ATEM switcher inputs keyed by the short name of the inputs (key is case insensitive)
    /// </summary>
    private readonly Dictionary<string, Input> _inputsByName;

    /// <summary>
    /// List of ATEM switcher inputs keyed by the input ID of the inputs
    /// </summary>
    private readonly Dictionary<long, Input> _inputsById;

    /// <summary>
    /// List of ATEM switcher macros available keyed by the name of the macros (key is case insensitive)
    /// </summary>
    private readonly Dictionary<string, Macro> _macrosByName;

    /// <summary>
    /// List of ATEM switcher macros available keyed by the numeric ID of the macro
    /// </summary>
    private readonly Dictionary<uint, Macro> _macrosById;

    /// <summary>
    /// Returns the list of available ATEM switcher inputs cast as Input objects
    /// </summary>
    public List<Input> SwitcherInputs { get => [.. _inputsById.Values]; }

    /// <summary>
    /// Creates a new instance of this class, establishes a connection to an ATEM switcher and reads information from the device.
    /// </summary>
    /// <param name="atemIpAddress">The IP address where the ATEM switcher is expected to be found (default used in not supplied)</param>
    public AtemSwitcher(string atemIpAddress = "192.168.1.240")
    {
        // Connect to the _switcher, if it's where it should be...
        IpAddress = "0.0.0.0";
        IsReady = false;    // Until proven otherwise.
        _BMDSwitcherConnectToFailure reason = _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureNoResponse;
        _switcher = null;
        try
        {
            IBMDSwitcherDiscovery discovery = new CBMDSwitcherDiscovery();
            discovery.ConnectTo(atemIpAddress, out _switcher, out reason);
            IsReady = true;
            NotReadyReason = string.Empty;
            IpAddress = atemIpAddress;
        }
        catch (Exception ex)
        {
            // Swallow the exception. If there's no ATEM this error is expected.
            Console.WriteLine(ex.ToString());

            // Note the connection failure reason, if any...
            switch (reason)
            {
                case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureNoResponse:
                    NotReadyReason = "The Switcher did not respond after a connection attempt was made. Confirm the ATEM IP Address.";
                    break;
                case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureIncompatibleFirmware:
                    NotReadyReason = "The software on the Switcher is incompatible with the current version of the Switcher SDK.";
                    break;
                case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureCorruptData:
                    NotReadyReason = "Corrupt data was received during a connection attempt.";
                    break;
                case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureStateSync:
                    NotReadyReason = "State synchronisation failed during a connection attempt.";
                    break;
                case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureStateSyncTimedOut:
                    NotReadyReason = "State synchronisation timed out during a connection attempt.";
                    break;
                case _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureDeprecatedAfter_v7_3:
                    NotReadyReason = "Failure deprecated after version 7.3";
                    break;
                default:
                    IsReady = true;
                    NotReadyReason = string.Empty;
                    break;
            }
        }

        _inputsByName = [];
        _inputsById = [];
        _macrosByName = [];
        _macrosById = [];
        if (IsReady)
        {
            // Get the first mix effects block (ATEM TVS HD only has one)
            _me0 = GetMixEffectBlocks.First();

            // Get the _switcher inputs list...
            foreach (var input in GetSwitcherInputs.ToList<IBMDSwitcherInput>())
            {
                Input discoveredInput = new(input);
                _inputsById.Add(discoveredInput.InputID, discoveredInput);
                _inputsByName.Add(discoveredInput.ShortName.ToUpperInvariant().Trim(), discoveredInput);

                // NOTE: If the AUX output has no short name then it isn't really there.
                if ((discoveredInput.PortType == AtemSwitcherPortType.AuxOutput) && discoveredInput.ShortName.Length > 0)
                {
                    _auxOut = (IBMDSwitcherInputAux)input;
                }
                else if (discoveredInput.PortType == AtemSwitcherPortType.MixEffectBlockOutput
                    && string.Compare(discoveredInput.ShortName.ToUpperInvariant(), "PGM") == 0)
                {
                    _pgmOut = (IBMDSwitcherInput)input;
                }
                // Is this the Preview Output?
                else if (discoveredInput.PortType == AtemSwitcherPortType.MixEffectBlockOutput
                    && string.Compare(discoveredInput.ShortName.ToUpperInvariant(), "PVW") == 0)
                {
                    _pvwOut = (IBMDSwitcherInput)input;
                }
            }

            // Get the list of available ATEM macros
            IBMDSwitcherMacroPool switcherMacroPool = (IBMDSwitcherMacroPool)_switcher;
            switcherMacroPool.GetMaxCount(out uint macroCount);
            for (uint i = 0; i < macroCount; i++)
            {
                switcherMacroPool.GetName(i, out string macroName);
                if (macroName == null || macroName.Length == 0)
                {
                    macroName = "{null}";
                }
                switcherMacroPool.GetDescription(i, out string macroDescription);
                macroDescription ??= string.Empty;
                if (macroName != "{null}")
                {
                    Macro macro = new()
                    {
                        Index = i,
                        Name = macroName,
                        Description = macroDescription
                    };
                    _macrosByName.Add(macro.Name.ToUpperInvariant().Trim(), macro);
                    _macrosById.Add(macro.Index, macro);
                }
            }

            // Get the switcher audio mixer...
            try
            {
                _audioMixer = (IBMDSwitcherAudioMixer)_switcher;
                _fairlightMixer = null;
            }
            catch (Exception)   // No classic mixer supported.
            {
                _fairlightMixer = (IBMDSwitcherFairlightAudioMixer)_switcher;
                _audioMixer = null;
            }
        }
    }

    /// <summary>
    /// Checks to see whether a particular input is present on the ATEM switcher
    /// </summary>
    /// <param name="inputShortName">Short name of the input being sought (case insensitive)</param>
    /// <returns>True when the short name refers to a valid input, False otherwise</returns>
    public bool HasInput(string inputShortName)
    {
        return _inputsByName.ContainsKey(inputShortName.ToUpperInvariant().Trim());
    }

    /// <summary>
    /// Returns an Input object representing a given input on the ATEM switcher
    /// </summary>
    /// <param name="inputShortName">The short name of the input being sought (case insensitive)</param>
    /// <returns>A reference to the Input with the given short name or null if no such input exists</returns>
    public Input GetInputByName(string inputShortName)
    {
        if (_inputsByName.TryGetValue(inputShortName, out Input result))
        {
            return result;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a list strings representing the names of inputs to the ATEM switcher
    /// </summary>
    public List<string> GetInputShortNames()
    {
        return [.. _inputsById.Values
                              .Select(i => i.ShortName)
                              .OrderBy(x => x.ToUpperInvariant())];
    }

    /// <summary>
    /// Returns a filtered list strings representing the names of inputs to the ATEM switcher
    /// </summary>
    /// <param name="selectedPortType">The type of port being sought</param>
    public List<string> GetInputShortNames(AtemSwitcherPortType selectedPortType)
    {
        return [.. _inputsById.Values
                              .Where(i => i.PortType == selectedPortType)
                              .Select(i => i.ShortName)
                              .OrderBy(x => x.ToUpperInvariant())];
    }

    /// <summary>
    /// Whether or not the ATEM switcher has a macro with a given name
    /// </summary>
    /// <param name="macroName">Name of the macro being sought (case insensitive)</param>
    /// <returns>True if the indicated macro exists, false otherwise</returns>
    public bool HasMacro(string macroName)
    {
        return _macrosByName.ContainsKey(macroName.ToUpperInvariant().Trim());
    }

    /// <summary>
    /// Returns a list of the names of macros installed on the ATEM switcher
    /// </summary>
    /// <returns></returns>
    public List<string> GetMacroNames()
    {
        return [.. _macrosById.Values
                              .Select(m => m.Name)
                              .OrderBy(x => x.ToUpperInvariant())];
    }

    /// <summary>
    /// Gets the Input that is presently assigned to the ATEM Preview feed
    /// </summary>
    /// <returns>The active Preview input or null if there is none.</returns>
    public Input GetPreviewInput()
    {
        Input result = null;

        if (_me0 is not null && _pvwOut is not null)
        {
            _me0.GetPreviewInput(out long inputId);
            _inputsById.TryGetValue(inputId, out result);
        }

        return result;
    }

    /// <summary>
    /// Sets the input to be assigned to the Preview feed
    /// </summary>
    /// <param name="inputId">The Input ID of the feed to be assigned</param>
    public void SetPreviewInput(long inputId)
    {
        if (_inputsById.ContainsKey(inputId))
        {
            _me0.SetPreviewInput(inputId);
        }
    }

    /// <summary>
    /// Sets the input to be assigned to the Preview feed
    /// </summary>
    /// <param name="inputShortName">The Short Name of the feed to be assigned (case insensitive)</param>
    public void SetPreviewInput(string inputShortName)
    {
        if (_inputsByName.TryGetValue(inputShortName, out var input))
        {
            SetPreviewInput(input.InputID);
        }
    }

    /// <summary>
    /// Sets the input to be assigned to the Preview feed
    /// </summary>
    /// <param name="input">The Input to preview</param>
    public void SetPreviewInput(Input input)
    {
        SetPreviewInput(input.InputID);
    }

    /// <summary>
    /// Gets the Input that is presently assigned to the ATEM Program feed
    /// </summary>
    /// <returns>The active Program input or null if there is none.</returns>
    public Input GetProgramInput()
    {
        Input result = null;

        if (_me0 is not null && _pgmOut is not null)
        {
            _me0.GetProgramInput(out long inputId);
            _inputsById.TryGetValue(inputId, out result);
        }

        return result;
    }

    /// <summary>
    /// Sets the input to be assigned to the Program feed
    /// </summary>
    /// <param name="inputId">The Input ID of the feed to be assigned</param>
    public void SetProgramInput(Input input)
    {
        SetProgramInput(input.InputID);
    }

    /// <summary>
    /// Sets the input to be assigned to the Program feed
    /// </summary>
    /// <param name="inputId">The Input ID of the feed to be assigned</param>
    public void SetProgramInput(long inputId)
    {
        if (_inputsById.ContainsKey(inputId))
        {
            _me0.SetProgramInput(inputId);
        }
    }

    /// <summary>
    /// Sets the input to be assigned to the Program feed
    /// </summary>
    /// <param name="inputShortName">The Short Name of the feed to be assigned (case insensitive)</param>
    public void SetProgramInput(string inputShortName)
    {
        if (_inputsByName.TryGetValue(inputShortName, out var input))
        {
            SetProgramInput(input.InputID);
        }
    }

    /// <summary>
    /// Gets the Input that is currently assigned to the Aux Output feed
    /// </summary>
    public Input GetAuxInput()
    {
        Input result = null;

        if (_auxOut is not null)
        {
            _auxOut.GetInputSource(out long inputId);
            _inputsById.TryGetValue(inputId, out result);
        }

        return result;
    }

    /// <summary>
    /// Sets the Input that is to be sent to the Aux Output feed
    /// </summary>
    /// <param name="input">The Input representing the source to be sent to Aux Out</param>
    public void SetAuxInput(Input input)
    {
        SetAuxInput(input.InputID);
    }

    /// <summary>
    /// Sets the Input that is to be sent to the Aux Output feed
    /// </summary>
    /// <param name="inputId">The Input ID representing the source to be sent to Aux Out</param>
    public void SetAuxInput(long inputId)
    {
        if (_inputsById.ContainsKey(inputId))
        {
            _auxOut.SetInputSource(inputId);
        }
    }

    /// <summary>
    /// Sets the Input that is to be sent to the Aux Output feed
    /// </summary>
    /// <param name="inputShortName">The short name of the source to be sent to Aux Out</param>
    public void SetAuxInput(string inputShortName)
    {
        if (_inputsByName.TryGetValue(inputShortName, out var input))
        {
            SetAuxInput(input.InputID);
        }
    }

    /// <summary>
    /// Send the Preview feed to the Aux Out port
    /// </summary>
    public void SetAuxToPreview()
    {
        _pvwOut.GetInputId(out long pvwInputId);
        SetAuxInput(pvwInputId);
    }

    /// <summary>
    /// Send the Program feed to the Aux Out port
    /// </summary>
    public void SetAuxToProgram()
    {
        _pgmOut.GetInputId(out long pgmInputId);
        SetAuxInput(pgmInputId);
    }

    /// <summary>
    /// Perform a CUT transition on the first Mix Effects block
    /// </summary>
    public void PerformCut()
    {
        _me0.PerformCut();
    }

    /// <summary>
    /// Perform an AUTO transition on the first Mix Effects block
    /// </summary>
    public void PerformAuto()
    {
        _me0.PerformAutoTransition();
    }

    /// <summary>
    /// Perform a Fade to Black transition on the first Mix Effects block
    /// </summary>
    public void PerformFadeToBlack()
    {
        _me0.PerformFadeToBlack();
    }

    /// <summary>
    /// Run a given ATEM macro
    /// </summary>
    /// <param name="macroIndex">The index ID of the macro to be run</param>
    public void RunMacro(uint macroIndex)
    {
        // Use the index
        IBMDSwitcherMacroControl switcherMacroControl = (IBMDSwitcherMacroControl)_switcher;
        switcherMacroControl.Run(macroIndex);
    }

    /// <summary>
    /// Run a given ATEM macro
    /// </summary>
    /// <param name="macroName">The name of the macro to be run (case insensitive)</param>
    public void RunMacro(string macroName)
    {
        if (_macrosByName.TryGetValue(macroName, out Macro value))
        {
            // Get the index
            uint macroIndex = value.Index;
            RunMacro(macroIndex);
        }
    }

    /// <summary>
    /// Internal helper for retrieving ME blocks from the ATEM API in the form of an IEnumerable
    /// </summary>
    private IEnumerable<IBMDSwitcherMixEffectBlock> GetMixEffectBlocks
    {
        get
        {
            // Create a mix effect block iterator
            _switcher.CreateIterator(typeof(IBMDSwitcherMixEffectBlockIterator).GUID, out IntPtr meIteratorPtr);
            if (Marshal.GetObjectForIUnknown(meIteratorPtr) is not IBMDSwitcherMixEffectBlockIterator meIterator)
            {
                yield break;
            }

            // Iterate through all mix effect blocks
            while (true)
            {
                meIterator.Next(out IBMDSwitcherMixEffectBlock me);
                if (me != null)
                {
                    yield return me;
                }
                else
                {
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Internal helper for retrieving inputs from the ATEM switcher in the form of an IEnumerable
    /// </summary>
    private IEnumerable<IBMDSwitcherInput> GetSwitcherInputs
    {
        get
        {
            // Create an input iterator
            _switcher.CreateIterator(typeof(IBMDSwitcherInputIterator).GUID, out IntPtr inputIteratorPtr);
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

    /// <summary>
    /// Sets the main program output volume for the current ATEM mixer
    /// </summary>
    /// <param name="volume">Volume setting in dB (-60.0 is negative infinity)</param>
    public void SetProgramVolume(double volume)
    {
        // Ensure that the new volume value is in a legal range
        volume = Math.Min(10.0, Math.Max(-60.0, volume));
        // Set the volume using whichever COM object is appropriate...
        _audioMixer?.SetProgramOutGain(volume);
        _fairlightMixer?.SetMasterOutFaderGain(volume);
    }

    /// <summary>
    /// Gets the main program output volume for the current ATEM mixer
    /// </summary>
    /// <returns>The program (main) volume in decibels</returns>
    public double GetProgramVolume()
    {
        if (_audioMixer is not null)
        {
            _audioMixer.GetProgramOutGain(out double volume);
            return volume;
        }
        else if (_fairlightMixer is not null) 
        {
            _fairlightMixer.GetMasterOutFaderGain(out double gain);
            return gain;
        }
        else
        {
            return 0.0; // Assume unity gain.
        }
    }

    /// <summary>
    /// Gets a list of ATEM audio input ports for the currently attached _switcher.
    /// Note: The XLR input is the input with portType == bmdSwitcherExternalPortTypeXLR
    /// Using: .GetCurrentExternalPortType(out _BMDSwitcherExternalPortType portType)
    /// </summary>
    private IEnumerable<IBMDSwitcherAudioInput> GetSwitcherAudioInputs
    {
        get
        {
            // Create an input iterator
            _audioMixer.CreateIterator(typeof(IBMDSwitcherAudioInputIterator).GUID, out IntPtr inputIteratorPtr);
            if (Marshal.GetObjectForIUnknown(inputIteratorPtr) is not IBMDSwitcherAudioInputIterator inputIterator)
            {
                yield break;
            }
            // Scan through all inputs
            while (true)
            {
                inputIterator.Next(out IBMDSwitcherAudioInput input);
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
}
