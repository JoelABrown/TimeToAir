using System.Net;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Mooseware.Tachnit.WebPresenterApi
{
    /// <summary>
    /// Blackmagic Design WebPresenter Streaming Bridge REST API Control Utility
    /// </summary>
    public class WebPresenterController
    {
        private readonly Serilog.ILogger _logger = Log.ForContext<WebPresenterController>();
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// IP Address of the WebPresenter device. Used for sending REST API commands
        /// </summary>
        public IPAddress IpAddress { get; private set; }

        /// <summary>
        /// Instantiates a new object of this type
        /// </summary>
        /// <param name="ipAddress">The IP address of the BMD WebPresenter device. Used for sending REST commands</param>
        /// <param name="httpClientFactory">Http Client Factory to use for REST commands</param>
        public WebPresenterController(string ipAddress, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            IpAddress = IPAddress.Parse("127.0.0.1");

            if (IPAddress.TryParse(ipAddress, out IPAddress? parsedIp))
            {
                IpAddress = parsedIp;
            }
            else
            {
                IpAddress = IPAddress.Parse("127.0.0.1");
            }
        }

        /// <summary>
        /// Checks to see whether the streaming bridge is responding to general status requests
        /// </summary>
        /// <returns>True when a status request returns a reasonable result, false otherwise</returns>
        public async Task<bool> Ping()
        {
            bool result = false;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1/"
                           + "system";

                var response = await httpClient.GetAsync(url, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.Debug("Request: " + url);
                    _logger.Debug("Response: {@Result}", responseBody);

                    SystemStatus? systemStatusResult =
                        JsonSerializer.Deserialize<SystemStatus>(responseBody);

                    if (systemStatusResult != null && systemStatusResult.VideoFormat.Width > 0)
                    {
                        result = true;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=false)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.Ping()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Checks the current status of the live stream
        /// </summary>
        /// <returns>Current status results</returns>
        public async Task<LiveStreamStatusResult> GetLivestreamStatus()
        {
            LiveStreamStatusResult result = new();

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0";

                var response = await httpClient.GetAsync(url, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.Debug("Request: " + url);
                    _logger.Debug("Response: {@Result}", responseBody);

                    LiveStreamStatusResult? liveStreamStatus =
                        JsonSerializer.Deserialize<LiveStreamStatusResult>(responseBody);

                    if (liveStreamStatus != null)
                    {
                        result = liveStreamStatus;
                    }
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=default/empty status object)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetLivestreamStatus()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Checks the streaming bridge to see whether the stream is currently live
        /// </summary>
        /// <returns>True when the stream is live, false otherwise</returns>
        public async Task<bool> GetIsLivestreamRunning()
        {
            bool result = false;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0/start";

                var response = await httpClient.GetAsync(url, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.Debug("Request: " + url);
                    _logger.Debug("Response: {@Result}", responseBody);

                    result = responseBody.Equals("true");
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=false)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetIsLivestreamRunning()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Checks the streaming bridge to see whether the stream is currently stopped
        /// </summary>
        /// <returns>True if the stream is currently stopped, false otherwise</returns>
        public async Task<bool> GetIsLivestreamStopped()
        {
            bool result = false;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0/stop";

                var response = await httpClient.GetAsync(url, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.Debug("Request: " + url);
                    _logger.Debug("Response: {@Result}", responseBody);

                    result = responseBody.Equals("true");
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=false)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetIsLivestreamStopped()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Gets the configuration details of the currently selected streaming platform
        /// </summary>
        /// <returns>Configuration information</returns>
        public async Task<ActivePlatformConfiguration> GetActivePlatformConfiguration()
        {
            ActivePlatformConfiguration result = new();

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0/activePlatform";

                var response = await httpClient.GetAsync(url, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.Debug("Request: " + url);
                    _logger.Debug("Response: {@Result}", responseBody);

                    ActivePlatformConfiguration? activePlatformConfiguration =
                        JsonSerializer.Deserialize<ActivePlatformConfiguration>(responseBody);

                    if (activePlatformConfiguration != null)
                    {
                        result = activePlatformConfiguration;
                    }
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=default/empty configuration object)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetActivePlatformConfiguration()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Set the active platform configuration settings of the WebPresenter
        /// </summary>
        /// <param name="key">The streaming key value used to authenticate with the streaming platform</param>
        /// <param name="platform">The name of the platform profile to be used by the WebPresenter</param>
        /// <param name="quality">The name of the quality setting to use for streaming</param>
        /// <param name="server">The name of the server profile to use (e.g. Primary, Secondary)</param>
        /// <returns></returns>
        public async Task<bool> SetActivePlatformConfiguration(string? key = null, string? platform = null, string? quality = null, string? server = null)
        {
            bool result = false;
            try {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0/activePlatform";

                // Prepare the content for the request
                ActivePlatformConfiguration config = new();
                // Do we need to read the current configuration as a baseline?
                if (key == null || platform == null || quality == null || server == null)
                {
                    config = await GetActivePlatformConfiguration();
                }
                // Overlay any provided parameters
                if (key != null)
                {
                    config.Key = key;
                }
                if (platform != null)
                {
                    config.Platform = platform;
                }
                if (quality != null)
                {
                    config.Quality = quality;
                }
                if (server != null)
                {
                    config.Server = server;
                }

                var contentJson = JsonSerializer.Serialize(config);
                var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    result = true;
                }
            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=false)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.SetActivePlatformConfiguration()");
                throw;
            }
            return result;

        }

        /// <summary>
        /// Gets a list of the defined platforms which the WebPresenter can send the stream to
        /// </summary>
        /// <returns>A list of the platform (profile) names</returns>
        public async Task<List<String>> GetPlatformList()
        {
            List<String> result = [];

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString() ////+ ":" + Port.ToString()
                           + "/control/api/v1"
                           + "/livestreams/platforms";

                var response = await httpClient.GetAsync(url, CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.Debug("Request: " + url);
                    _logger.Debug("Response: {@Result}", responseBody);

                    result = JsonSerializer.Deserialize<List<String>>(responseBody) ?? [];
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=empty list)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetPlatformList()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Starts the live stream based on the currently selected profile/configuration
        /// </summary>
        /// <returns>True if the command is accepted by the WebPresenter, false otherwise</returns>
        public async Task<bool> StartLivestream()
        {
            bool result = false;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0/start";

                var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync(url, content);

                // NOTE: There is no expected response body (HTTP 204) on success
                if (response.IsSuccessStatusCode)
                {
                    result = true;
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=false)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetIsLivestreamRunning()");
                throw;
            }
            return result;
        }

        /// <summary>
        /// Stops the live stream (if it is in progress)
        /// </summary>
        /// <returns>True if the command is accepted by the WebPresenter, false otherwise</returns>
        public async Task<bool> StopLivestream()
        {
            bool result = false;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                string url = @"http://"
                           + IpAddress.ToString()
                           + "/control/api/v1"
                           + "/livestreams/0/stop";

                var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync(url, content);

                // NOTE: There is no expected response body (HTTP 204) on success
                if (response.IsSuccessStatusCode)
                {
                    result = true;
                }

            }
            catch (HttpRequestException)
            {
                // Swallow this, but treat it as bad news (result=false)
            }
            catch (Exception ex)
            {
                // Anything else wants logging and throwing...
                _logger.Error(ex, "Error caught in WebPresenterController.GetIsLivestreamRunning()");
                throw;
            }
            return result;
        }

        //// NOTE: YAGNI (probably)
        ////public async Task<bool> GetPlatformConfiguration(string platformName)
        ////{
        ////    bool result = false;
        ////    try
        ////    {
        ////        using var httpClient = _httpClientFactory.CreateClient();
        ////        httpClient.DefaultRequestHeaders.Accept.Clear();
        ////        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        ////        string url = @"http://"
        ////                   + IpAddress.ToString() ////+ ":" + Port.ToString()
        ////                   + "/control/api/v1"
        ////                   + "/livestreams/platforms/"
        ////                   + Uri.EscapeDataString(platformName);
        ////        var response = await httpClient.GetAsync(url, CancellationToken.None);
        ////        if (response.IsSuccessStatusCode)
        ////        {
        ////            var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
        ////            _logger.Debug("Request: " + url);
        ////            _logger.Debug("Response: {@Result}", responseBody);
        ////            result = true;
        ////        }
        ////    }
        ////    catch (HttpRequestException)
        ////    {
        ////        // Swallow this, but treat it as bad news (result=false)
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        // Anything else wants logging and throwing...
        ////        _logger.Error(ex, "Error caught in WebPresenterController.GetPlatformConfiguration()");
        ////        throw;
        ////    }
        ////    return result;
        ////}
    }
}
