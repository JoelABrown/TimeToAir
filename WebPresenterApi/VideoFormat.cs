using System.Text.Json.Serialization;

namespace Mooseware.Tachnit.WebPresenterApi
{
    /// <summary>
    /// BMD WebPresenter video format details
    /// </summary>
    public class VideoFormat
    {
        /// <summary>
        /// The frames per second of the streamed video
        /// </summary>
        [JsonPropertyName("frameRate")]
        public string FrameRate { get; set; } = string.Empty;
        /// <summary>
        /// Height of the streamed video in pixels
        /// </summary>
        [JsonPropertyName("height")]
        public int Height { get; set; } = 0;
        /// <summary>
        /// Whether or not the streamed video is interlaced
        /// </summary>
        [JsonPropertyName("interlaced")]
        public bool Interlaced { get; set; } = false;
        /// <summary>
        /// The aggregate profile name of the video format (size and frame rate) as a string 
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// The width of the streamed video in pixels
        /// </summary>
        [JsonPropertyName("width")]
        public int Width { get; set; } = 0;
    }
}
