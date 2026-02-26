using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mooseware.Tachnit.BmdHyperdeckController
{
    /// <summary>
    /// Used to control a Blackmagic Design HyperDeck Device via TCP API
    /// </summary>
    public class HyperdeckController:IDisposable
    {
        private readonly Serilog.ILogger _logger = Log.ForContext<HyperdeckController>();

        /// <summary>
        /// IP Address for the BMD HyperDeck used for TCP commands
        /// </summary>
        public IPAddress IpAddress { get; private set; }

        /// <summary>
        /// Storage Slot 1 Free Space Status
        /// </summary>
        public StorageSlotStatus Slot1Status { get; private set; } = StorageSlotStatus.Unknown;
        /// <summary>
        /// Storage Slot 2 Free Space Status
        /// </summary>
        public StorageSlotStatus Slot2Status { get; private set; } = StorageSlotStatus.Unknown;
        /// <summary>
        /// Last known transport status of the playback/record "head" (Call GetTransportInfoAsync to refresh)
        /// </summary>
        public TransportStatus LastKnownTransportStatus { get; private set; } = TransportStatus.Unknown;

        /// <summary>
        /// Port used for TCP commands
        /// </summary>
        public int Port { get; private set; } = 9993;

        private const string _OkResponse = "200 ok";

        private readonly Socket _socket;
        private bool disposedValue;

        public HyperdeckController(string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? parsedIp))
            {
                IpAddress = parsedIp;
            }
            else
            {
                IpAddress = IPAddress.Parse("127.0.0.1");
            }
            IPEndPoint remoteEP = new(IpAddress, Port);
            _socket = new(IpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = 2000  // Wait 2 seconds to receive, max.
            };
            _logger.Debug("HyperdeckController instantiated for {0}:{1}", IpAddress.ToString(), Port);
            try
            {
                _socket.Connect(remoteEP);
                byte[] buffer = new byte[4096];
                _socket.Receive(buffer);
                string response = Encoding.ASCII.GetString(buffer);
                if (response.StartsWith("120 connection rejected"))
                {
                    _logger.Error("HyperdeckController TCP connection rejected");
                    throw new ApplicationException("Unable to connect to the BMD HyperDeck");
                }

                // Make sure the HyperDeck will accept remote control.
                if (!SendCommand("remote: enable: true").StartsWith(_OkResponse))
                {
                    _logger.Error("HyperdeckController: BMD HyperDeck not configured for remote control");
                    throw new ApplicationException("Unable to remote control the BMD HyperDeck");
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController constructor");
                // Swallow this error. It's expected if the HyperDeck isn't there;
            }
        }

        /// <summary>
        /// Attempt to connect to the BMD HyperDeck to see if it can be done (uses the Ping command)
        /// </summary>
        /// <returns>True if the connection is successful, false otherwise</returns>
        public async Task<bool> TestConnectionIsOKAsync()
        {
            bool result = false;
            _logger.Debug("HyperdeckController.TestConnectionIsOKAsync()");
            try
            {
                var resultString = await SendCommandAsync("ping");
                _logger.Debug("TestConnectionIsOKAsync()={0} ({1})", resultString, resultString.StartsWith(_OkResponse));
                result = resultString.StartsWith(_OkResponse);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.TestConnectionIsOKAsync()");
                // Swallow the error. This is expected if the HyperDeck is not connected.
            }
            return result;
        }

        /// <summary>
        /// Sets the HyperDeck to Preview Mode (required for monitoring recording at the output)
        /// </summary>
        public void SetPreviewMode()
        {
            _logger.Debug("HyperdeckController.SetPreviewMode()");
            try
            {
                SendCommand("preview: enable: true");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.SetPreviewMode()");
                throw;
            }
        }

        /// <summary>
        /// Get information about the play/record "head" of the HyperDeck. Also updates the LastKnownTransportStatus property
        /// </summary>
        /// <returns>Status information as a list of keys and values in string form</returns>
        public async Task<string> GetTransportInfoAsync()
        {
            string result;
            _logger.Debug("HyperdeckController.GetTransportInfoAsync()");
            try
            {
                var response = await SendCommandAsync("transport info");
                string statusValue = GetResponseParameter(response, "status");
                LastKnownTransportStatus = statusValue.ToLower() switch
                {
                    "stopped" => TransportStatus.Stopped,
                    "preview" => TransportStatus.Preview,
                    "record" => TransportStatus.Record,
                    _ => TransportStatus.Unknown
                };
                result = response ?? string.Empty;
                _logger.Debug("GetTransportInfoAsync()={0} (LastKnownTransportStatus={1})", result, LastKnownTransportStatus.ToString());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.GetTransportInfoAsync()");
                throw;
            }
            
            return result;
        }

        /// <summary>
        /// Gets the total amount of available recording space on the HyperDeck's 2 SD card slots. Also updates Slot1Status and Slot2Status.
        /// </summary>
        /// <returns>The total available recording time to the nearest minute</returns>
        public async Task<int> GetRemainingCapacityInMinutesAsync()
        {
            // Make a note of whether one slot or the other (or both) are full
            Slot1Status = StorageSlotStatus.Unknown;
            Slot2Status = StorageSlotStatus.Unknown;

            int totalRemainingCapacity = 0;
            const string recTimeInfoLabel = "recording time";

            _logger.Debug("HyperdeckController.GetRemainingCapacityInMinutesAsync()");

            try
            {
                // Get the info for the first slot...
                string response = await SendCommandAsync("slot info: slot id: 1");
                string recTime = GetResponseParameter(response, recTimeInfoLabel);
                if (int.TryParse(recTime, out int recTimeInSeconds))
                {
                    Slot1Status = (recTimeInSeconds > 0) ? StorageSlotStatus.NotFull : StorageSlotStatus.Full;
                    totalRemainingCapacity += recTimeInSeconds;
                    _logger.Verbose("Hyperdeck Slot 1 remaining seconds: {0}", recTimeInSeconds);
                }

                // Get the info for the second slot...
                response = await SendCommandAsync("slot info: slot id: 2");
                recTime = GetResponseParameter(response, recTimeInfoLabel);
                if (int.TryParse(recTime, out recTimeInSeconds))
                {
                    Slot2Status = (recTimeInSeconds > 0) ? StorageSlotStatus.NotFull : StorageSlotStatus.Full;
                    totalRemainingCapacity += recTimeInSeconds;
                    _logger.Verbose("Hyperdeck Slot 2 remaining seconds: {0}", recTimeInSeconds);
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.GetRemainingCapacityInMinutesAsync()");
                throw;
            }
            // Return the total remainging capacity to the nearest (full) minute.
            _logger.Debug("Hyperdeck remaining minutes: {0}", totalRemainingCapacity / 60);
            return totalRemainingCapacity / 60;
        }

        /// <summary>
        /// Starts recording on the BMD HyperDeck
        /// </summary>
        /// <param name="clipName">The name of the recording file (optional)</param>
        public void StartRecording(string clipName = "")
        {
            _logger.Debug("HyperdeckController.StartRecording({0})", clipName);
            try
            {
                if (clipName == string.Empty)
                {
                    SendCommand("record");
                }
                else
                {
                    // Make sure there are no illegal characters in the file
                    // name and that the file has an extension...
                    string normalizedClipName = string.Join("_", clipName.Split(Path.GetInvalidFileNameChars()));
                    if (!normalizedClipName.ToLower().EndsWith(".mp4"))
                    {
                        normalizedClipName += ".mp4";
                    }
                    SendCommand("record: name: " + normalizedClipName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.StartRecording()");
                throw;
            }

        }

        /// <summary>
        /// Stops recording on the BMD HyperDeck
        /// </summary>
        public void StopRecording()
        {
            _logger.Debug("HyperdeckController.StopRecording()");
            try
            {
                SendCommand("stop");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.StopRecording()");
                throw;
            }
        }

        /// <summary>
        /// Sends a TCP command to the BMD HyperDeck synchronously
        /// </summary>
        /// <param name="command">The contents of the command to be sent</param>
        /// <returns>The immediate TCP response to the command</returns>
        private string SendCommand(string command)
        {
            string result;
            _logger.Debug("HyperdeckController.SendCommand({0})", command);

            byte[] commandBuffer = Encoding.ASCII.GetBytes(command + Environment.NewLine);
            byte[] response = new byte[4096];

            try
            {
                int bytesSent = _socket.Send(commandBuffer);
                int bytesReceived = _socket.Receive(response);
                result = Encoding.ASCII.GetString(response, 0, bytesReceived);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.SendCommand()");
                throw;
            }

            return result;
        }

        /// <summary>
        /// Sends a TCP command to the BMD HyperDeck asynchronously
        /// </summary>
        /// <param name="command">The contents of the command to be sent</param>
        /// <returns>The immediate TCP response to the command</returns>
        private async Task<string> SendCommandAsync(string command)
        {
            string result;
            _logger.Debug("HyperdeckController.SendCommandAsync({0})", command);

            byte[] commandBuffer = Encoding.ASCII.GetBytes(command + Environment.NewLine);
            byte[] response = new byte[4096];

            try
            {
                int bytesSent = await _socket.SendAsync(commandBuffer);
                int bytesReceived = await _socket.ReceiveAsync(response);
                result = Encoding.ASCII.GetString(response, 0, bytesReceived);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error trapped in HyperdeckController.SendCommandAsync()");
                throw;
            }

            return result;
        }

        /// <summary>
        /// Extracts any given parameter value from a response string
        /// </summary>
        /// <param name="wholeMessage">The whole (response) message</param>
        /// <param name="parameterName">The name of the specific parameter being sought</param>
        /// <returns>The parameter value or empty string if not found</returns>
        private static string GetResponseParameter(string wholeMessage, string parameterName)
        {
            foreach (var line in wholeMessage.Split("\r\n"))
            {
                if (line.StartsWith(parameterName + ":"))
                {
                    return line[(parameterName.Length + 1)..].Trim();
                }
            }

            return String.Empty;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                    if (_socket != null)
                    {
                        if (_socket.Connected)
                        {
                            SendCommand("quit");
                        }
                        try
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                        }
                        catch (Exception)
                        {
                            // Swallow it.
                        }
                        finally
                        {
                            _socket.Close();
                            _socket.Dispose();
                        }
                    }
                }

                // Free unmanaged resources (unmanaged objects) and override finalizer
                // Set large fields to null
                disposedValue = true;
            }
        }

        // // Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~BmdHyperDeckCnx()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Indicator of whether a HyperDeck storage slot has remaining recording space
    /// </summary>
    public enum StorageSlotStatus
    {
        /// <summary>
        /// Not yet determined
        /// </summary>
        Unknown,
        /// <summary>
        /// Storage slot has remaining recording capacity
        /// </summary>
        NotFull,
        /// <summary>
        /// Storage slot has no recording capacity left
        /// </summary>
        Full
    }

    /// <summary>
    /// Status of the playback/record "head"
    /// </summary>
    public enum TransportStatus
    {
        Unknown,
        Stopped,
        Preview,
        Record
    }
}
