using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Security;
using System.Text;
using BMDSwitcherAPI;

namespace Mooseware.Tachnit.AtemApi
{
    public class Switcher
    {
        public bool IsReady { get; private set; }
        public string NotReadyReason { get; private set; }
        public string Input1Name { get; private set; }
        public bool Input1Ready { get; private set; }
        public string Input2Name { get; private set; }
        public bool Input2Ready { get; private set; }
        public string InputMP1Name { get; private set; }
        public bool InputMP1Ready { get; private set; }
        public string InputMP2Name { get; private set; }
        public bool InputMP2Ready { get; private set; }
        public string InputClipName { get; private set; }
        public bool InputClipReady { get; private set; }

        private readonly AtemSwitcher _atem;
        private readonly IBMDSwitcherMixEffectBlock _me0;
        private readonly IBMDSwitcherInputAux _auxOut;
        private readonly IBMDSwitcherInput _pgmOut;
        private readonly IBMDSwitcherInput _pvwOut;

        private readonly long _input1Id;
        private readonly long _input2Id;
        private readonly long _inputMp1Id;
        private readonly long _inputMp2Id;
        private readonly long _inputClipId;

        private readonly List<Macro> _macros;

        public Switcher(string atemIpAddress = "192.168.1.240", string input1 = "CAM1", string input2 = "CAM2",
            string inputMp1 = "MP1", string inputMp2 = "MP2", string inputClip = "PC4")
        {
            // Note the input preferences...
            Input1Name = input1;
            Input1Ready = false;    // Until proven otherwise.
            Input2Name = input2;
            Input1Ready = false;
            InputMP1Name = inputMp1;
            InputMP1Ready = false;
            InputMP2Name = inputMp2;
            InputMP2Ready = false;
            InputClipName = inputClip;
            InputClipReady = false;

            // Connect to the switcher, if it's where it should be...
            IsReady = false;    // Until proven otherwise.
            _BMDSwitcherConnectToFailure reason = _BMDSwitcherConnectToFailure.bmdSwitcherConnectToFailureNoResponse;
            IBMDSwitcher switcher = null;
            try
            {
                IBMDSwitcherDiscovery discovery = new CBMDSwitcherDiscovery();
                discovery.ConnectTo(atemIpAddress, out switcher, out reason);
                IsReady = true;
                NotReadyReason = string.Empty;
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

            _macros = [];
            if (IsReady)
            {
                // Get a switcher object for operating the switcher...
                _atem = new AtemSwitcher(switcher);

                // Get the first mix effects block (ATEM TVS HD only has one)
                _me0 = _atem.MixEffectBlocks.First();

                // Get the switcher inputs list...
                foreach (var input in _atem.SwitcherInputs.ToList<IBMDSwitcherInput>())
                {
                    // What do we want to know about this input?
                    input.GetShortName(out string shortName);
                    input.GetInputId(out long inputId);
                    input.GetPortType(out _BMDSwitcherPortType portType);

                    // Is this the first input?
                    if (string.Compare(shortName.ToUpperInvariant(), input1.ToUpperInvariant()) == 0)
                    {
                        Input1Name = shortName;     // Note using the ATEM version just in case there's a variation in casing.
                        _input1Id = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeExternal) == _BMDSwitcherPortType.bmdSwitcherPortTypeExternal)
                        {
                            Input1Ready = true;
                        }
                    }
                    // Is this the second input?
                    else if (string.Compare(shortName.ToUpperInvariant(), input2.ToUpperInvariant()) == 0)
                    {
                        Input2Name = shortName;     // Note using the ATEM version just in case there's a variation in casing.
                        _input2Id = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeExternal) == _BMDSwitcherPortType.bmdSwitcherPortTypeExternal)
                        {
                            Input2Ready = true;
                        }
                    }
                    // Is this the MP1 input?
                    else if (string.Compare(shortName.ToUpperInvariant(), inputMp1.ToUpperInvariant()) == 0)
                    {
                        InputMP1Name = shortName;   // Note using the ATEM version just in case there's a variation in casing.
                        _inputMp1Id = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill) == _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill)
                        {
                            InputMP1Ready = true;
                        }
                    }
                    // Is this the MP2 input?
                    else if (string.Compare(shortName.ToUpperInvariant(), inputMp2.ToUpperInvariant()) == 0)
                    {
                        InputMP2Name = shortName;   // Note using the ATEM version just in case there's a variation in casing.
                        _inputMp2Id = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill) == _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill)
                        {
                            InputMP2Ready = true;
                        }
                    }
                    // Is this the Clip input?
                    else if (string.Compare(shortName.ToUpperInvariant(), inputClip.ToUpperInvariant()) == 0)
                    {
                        InputClipName = shortName;     // Note using the ATEM version just in case there's a variation in casing.
                        _inputClipId = inputId;
                        if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeExternal) == _BMDSwitcherPortType.bmdSwitcherPortTypeExternal)
                        {
                            InputClipReady = true;
                        }
                    }
                    // Is this the Aux Output?
                    else if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeAuxOutput) == _BMDSwitcherPortType.bmdSwitcherPortTypeAuxOutput)
                    {
                        _auxOut = (IBMDSwitcherInputAux)input;
                    }
                    // Is this the Program Output?
                    else if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput) == _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput
                            && string.Compare(shortName.ToUpperInvariant(),"PGM")==0)
                    {
                        _pgmOut = (IBMDSwitcherInput)input;
                    }
                    // Is this the Preview Output?
                    else if ((portType & _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput) == _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput
                            && string.Compare(shortName.ToUpperInvariant(), "PVW") == 0)
                    {
                        _pvwOut = (IBMDSwitcherInput)input;
                    }
                }

                _macros = _atem.Macros;
            }
        }

        public void Take1Preview2()
        {
            // Take 1 / Preview 2, as long as the switcher and both of these inputs are ready.
            if (IsReady && Input1Ready && Input2Ready)
            {
                _me0.SetProgramInput(_input1Id);
                _me0.SetPreviewInput(_input2Id);
            }
        }

        public void Take2Preview1()
        {
            // Take 2 / Preview 1, as long as the switcher and both of these inputs are ready.
            if (IsReady && Input1Ready && Input2Ready)
            {
                _me0.SetProgramInput(_input2Id);
                _me0.SetPreviewInput(_input1Id);
            }
        }

        public void TakeMP1Preview1()
        {
            // Take MP1 / Preview 1, as long as the switcher and both of these inputs are ready.
            if (IsReady && InputMP1Ready && Input1Ready)
            {
                _me0.SetProgramInput(_inputMp1Id);
                _me0.SetPreviewInput(_input1Id);
            }
        }

        public void TakeMP2Preview1()
        {
            // Take MP2 / Preview 1, as long as the switcher and both of these inputs are ready.
            if (IsReady && InputMP2Ready && Input1Ready)
            {
                _me0.SetProgramInput(_inputMp2Id);
                _me0.SetPreviewInput(_input1Id);
            }
        }

        public void TakeClip()
        {
            // Preview tally the Clip and cut it to program, as long as the switcher and clip input are ready.
            if (IsReady && InputClipReady)
            {
                _me0.SetPreviewInput(_inputClipId);
                _me0.PerformCut();
            }
        }

        public List<string> ListSwitcherInputs
        {
            get
            {
                List<string> results = [];

                foreach (var input in _atem.SwitcherInputs.ToList<IBMDSwitcherInput>())
                {
                    // What do we want to know about this input?
                    input.GetShortName(out string shortName);
                    input.GetInputId(out long inputId);
                    input.GetPortType(out _BMDSwitcherPortType portType);

                    if (shortName != null && shortName.Length > 0)
                    {
                        results.Add("Short name=[" + shortName + "] InputId=[" + inputId.ToString() + "] "
                            + "PortType=[" + portType.ToString() + "] (" + ((long)portType).ToString() + ")");
                    }
                }

                return results;
            }
        }

        public void RunMacro(string macroName)
        {
            if (IsReady)
            {
                foreach (var macro in _macros)
                {
                    if (macro.Name.Equals(macroName))
                    {
                        _atem.RunMacro(macro.Index);
                        break;
                    }
                }
            }
        }

        public List<string> MacroNameList
        {
            get
            {
                List<string> result = [];

                foreach (var item in _macros)
                {
                    result.Add(item.Name);
                }

                return result;
            }
        }

        public string TempGetAuxOut()
        {
            string result = string.Empty;

            if (_atem != null)
            {
                if (_auxOut != null)
                {
                   //// _auxOut.GetInputSource(out long input);
                    ////result = "Aux=" + input.ToString();

                    _auxOut.GetInputSource(out long currentAuxOut);
                    if (currentAuxOut == GetInputId(_pgmOut))
                    {
                        _auxOut.SetInputSource(GetInputId(_pvwOut));
                        result = "AUX=Preview";
                    }
                    else
                    {
                        _auxOut.SetInputSource(GetInputId(_pgmOut));
                        result = "AUX=Program";
                    }
                }
                else
                {
                    result = "NO AUX";
                }
            }

            return result;

        }

        private static long GetInputId(IBMDSwitcherInput input)
        {
            input.GetInputId(out long id);
            return id;
        }

    }
}
