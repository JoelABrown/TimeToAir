using BMDSwitcherAPI;

namespace Mooseware.Tachnit.AtemApi;

/// <summary>
/// An ATEM Switcher logical or physical port
/// </summary>
public class Input
{
    /// <summary>
    /// Numeric identifier of the input port
    /// </summary>
    public long InputID { get; private set; }

    /// <summary>
    /// Short name assigned to the port
    /// </summary>
    public string ShortName { get; private set; }

    /// <summary>
    /// Long name assigned to the port
    /// </summary>
    public string LongName { get; private set; }

    /// <summary>
    /// The type of the port (based on a BMD enumeration)
    /// </summary>
    public AtemSwitcherPortType PortType { get; private set; }

    /// <summary>
    /// Create a new instance based on copying values from the BMD Switcher Input object
    /// </summary>
    /// <param name="input">The BMD Switcher Input object whose properties are to be recorded</param>
    public Input(IBMDSwitcherInput input)
    {
        // Get the properties from the provided input
        input.GetInputId(out long inputId);
        InputID = inputId;

        input.GetShortName(out string shortName);
        ShortName = shortName;

        input.GetLongName(out string longName);
        LongName = longName;

        input.GetPortType(out _BMDSwitcherPortType portType);
        AtemSwitcherPortType atemPortType = EnumXlat.XlatToAtemPortType(portType);
        PortType = atemPortType;
    }

    public override string ToString()
    {
        return ShortName;
    }
}
