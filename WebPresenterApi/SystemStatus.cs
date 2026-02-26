using System.Text.Json.Serialization;

namespace Mooseware.Tachnit.WebPresenterApi
{
    /// <summary>
    /// Response JSON root for the system status inquiry API
    /// </summary>
    public class SystemStatus
    {
        /// <summary>
        /// Streaming video format details
        /// </summary>
        [JsonPropertyName("videoFormat")]
        public VideoFormat VideoFormat { get; set; } = new();
    }
}
