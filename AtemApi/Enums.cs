using BMDSwitcherAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mooseware.Tachnit.AtemApi;

public enum AtemSwitcherPortType
{
    External,
    Black,
    ColorBars,
    ColorGenerator,
    MediaPlayerFill,
    MediaPlayerCut,
    SuperSource,
    MixEffectBlockOutput,
    AuxOutput,
    KeyCutOutput,
    Multiview,
    ExternalDirect
}

internal static class EnumXlat
{
    public static _BMDSwitcherPortType XlatToBmdPortType(AtemSwitcherPortType atemPortType)
    {
        return atemPortType switch
        {
            AtemSwitcherPortType.External => _BMDSwitcherPortType.bmdSwitcherPortTypeExternal,
            AtemSwitcherPortType.Black => _BMDSwitcherPortType.bmdSwitcherPortTypeBlack,
            AtemSwitcherPortType.ColorBars => _BMDSwitcherPortType.bmdSwitcherPortTypeColorBars,
            AtemSwitcherPortType.ColorGenerator => _BMDSwitcherPortType.bmdSwitcherPortTypeColorGenerator,
            AtemSwitcherPortType.MediaPlayerFill => _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill,
            AtemSwitcherPortType.MediaPlayerCut => _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerCut,
            AtemSwitcherPortType.SuperSource => _BMDSwitcherPortType.bmdSwitcherPortTypeSuperSource,
            AtemSwitcherPortType.MixEffectBlockOutput => _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput,
            AtemSwitcherPortType.AuxOutput => _BMDSwitcherPortType.bmdSwitcherPortTypeAuxOutput,
            AtemSwitcherPortType.KeyCutOutput => _BMDSwitcherPortType.bmdSwitcherPortTypeKeyCutOutput,
            AtemSwitcherPortType.Multiview => _BMDSwitcherPortType.bmdSwitcherPortTypeMultiview,
            AtemSwitcherPortType.ExternalDirect => _BMDSwitcherPortType.bmdSwitcherPortTypeExternalDirect,
            _ => _BMDSwitcherPortType.bmdSwitcherPortTypeExternal,
        };
    }

    public static AtemSwitcherPortType XlatToAtemPortType(_BMDSwitcherPortType bmdPortType)
    {
        return bmdPortType switch
        {
            _BMDSwitcherPortType.bmdSwitcherPortTypeExternal => AtemSwitcherPortType.External,
            _BMDSwitcherPortType.bmdSwitcherPortTypeBlack => AtemSwitcherPortType.Black,
            _BMDSwitcherPortType.bmdSwitcherPortTypeColorBars => AtemSwitcherPortType.ColorBars,
            _BMDSwitcherPortType.bmdSwitcherPortTypeColorGenerator => AtemSwitcherPortType.ColorGenerator,
            _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerFill => AtemSwitcherPortType.MediaPlayerFill,
            _BMDSwitcherPortType.bmdSwitcherPortTypeMediaPlayerCut => AtemSwitcherPortType.MediaPlayerCut,
            _BMDSwitcherPortType.bmdSwitcherPortTypeSuperSource => AtemSwitcherPortType.SuperSource,
            _BMDSwitcherPortType.bmdSwitcherPortTypeMixEffectBlockOutput => AtemSwitcherPortType.MixEffectBlockOutput,
            _BMDSwitcherPortType.bmdSwitcherPortTypeAuxOutput => AtemSwitcherPortType.AuxOutput,
            _BMDSwitcherPortType.bmdSwitcherPortTypeKeyCutOutput => AtemSwitcherPortType.KeyCutOutput,
            _BMDSwitcherPortType.bmdSwitcherPortTypeMultiview => AtemSwitcherPortType.Multiview,
            _BMDSwitcherPortType.bmdSwitcherPortTypeExternalDirect => AtemSwitcherPortType.ExternalDirect,
            _ => AtemSwitcherPortType.External,
        };
    }
}