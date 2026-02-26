using System.Text.Json.Serialization;

namespace Mooseware.Tachnit.WebPresenterApi
{
    /// <summary>
    /// Response JSON format for the live stream status inquiry API
    /// </summary>
    public class LiveStreamStatusResult
    {
        /// <summary>
        /// Stream target bit rate
        /// </summary>
        [JsonPropertyName("bitrate")]
        public int Bitrate { get; set; } = 0;
        /// <summary>
        /// Stream video format (size and frames per second)
        /// </summary>
        [JsonPropertyName("effectiveVideoFormat")]
        public string EffectiveVideoFormat { get; set; } = String.Empty;
        /// <summary>
        /// Indication of the current stream status
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("status")]
        public LiveStreamState Status {  get; set; } = LiveStreamState.Unknown;
    }

    /// <summary>
    /// Possible current status values for the WebPresenter live stream
    /// </summary>
    public enum LiveStreamState
    {
        Unknown,
        Idle,
        Connecting,
        Streaming,
        Flushing,
        Interrupted
    }
}
