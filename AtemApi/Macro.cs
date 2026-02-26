namespace Mooseware.Tachnit.AtemApi;

/// <summary>
/// An ATEM Switcher macro discovered on a connected ATEM
/// </summary>
public class Macro
{
    /// <summary>
    /// The numeric identifier of the macro
    /// </summary>
    public uint Index { get; set; }

    /// <summary>
    /// The name assigned to the macro
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// A free-form description assigned to the macro
    /// </summary>
    public string Description { get; set; }

    public Macro()
    {

    }

    public Macro(uint index, string name, string description)
    {
        Index = index;
        Name = name;
        Description = description;
    }

    public override string ToString()
    {
        return Name;
    }
}
