using System.Text.Json.Serialization;

namespace Mooseware.Tachnit.WebPresenterApi
{
    /// <summary>
    /// BMD WebPresenter API Active Platform Configuration settings
    /// </summary>
    public class ActivePlatformConfiguration
    {
        /// <summary>
        /// The streaming key used to authenticate with the streaming platform
        /// </summary>
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
        /// <summary>
        /// The name of the streaming platform (profile) used by the WebPresenter
        /// </summary>
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;
        /// <summary>
        /// The stream quality profile name used by the WebPresenter
        /// </summary>
        [JsonPropertyName("quality")]
        public string Quality { get; set; } = string.Empty;
        /// <summary>
        /// The stream server connection profile name used by the WebPresenter
        /// </summary>
        [JsonPropertyName("server")]
        public string Server { get; set; } = string.Empty;
    }
}
