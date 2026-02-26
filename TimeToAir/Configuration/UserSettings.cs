namespace Mooseware.TimeToAir.Configuration;

/// <summary>
/// Wrapper for the Properties.Settings.Default class that accesses app.config files for runtime user-editable settings.
/// </summary>
internal static class Config
{
    /// <summary>
    /// Returns the Properties.Settings.Default static class that provides app.config settings and file management.
    /// </summary>
    public static Properties.Settings User
    {
        get
        {
            return Properties.Settings.Default;
        }
    }
}
