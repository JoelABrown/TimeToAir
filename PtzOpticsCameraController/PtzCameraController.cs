using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Serilog;

namespace Mooseware.Tachnit.PtzOpticsCameraController;

/// <summary>
/// Used to control PtzOptics Cameras via VISCA commands
/// </summary>
public class PtzCameraController : IDisposable
{
    private readonly Serilog.ILogger _logger = Log.ForContext<PtzCameraController>();
    private bool disposedValue;
    private const int _receiveTimeout = 500;    // 250ms? receive timeout

    /// <summary>
    /// The IP Address of the camera to which commands are sent via TCP
    /// </summary>
    public IPAddress IpAddress { get; private set; }

    /// <summary>
    /// The port of the camera to which TCP commands are sent
    /// </summary>
    public int Port { get; private set; } = 5678;

    private static IPEndPoint? _remoteEP;
    private static Socket? _sender;

    private string _panValue = string.Empty;
    private string _tiltValue = string.Empty;
    private string _zoomValue = string.Empty;

    /// <summary>
    /// Indicates whether or not the camera is responding to connection requests
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Instantiates a connector to a single PtzOptics camera
    /// </summary>
    /// <param name="ipAddress">The IP address of the camera for VISCA control</param>
    public PtzCameraController(string ipAddress)
    {
        if (IPAddress.TryParse(ipAddress, out IPAddress? parsedIp))
        {
            IpAddress = parsedIp;
        }
        else
        {
            IpAddress = IPAddress.Parse("127.0.0.1");
        }
        try
        {
            IsConnected = true; // Unless we find out otherwise.
            _remoteEP = new(IpAddress, Port);
            _sender = new(IpAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = _receiveTimeout
            };
            _sender.Connect(_remoteEP);

            _logger.Debug("PtzCameraController instantiated for {0}:{1}", IpAddress.ToString(), Port);
        }
        catch (Exception ex)
        {
            _logger.Error("Error instantiating PtzOpticsCameraControllerAsync", ex);
            // Swallow the error. This is to be expected if the camera isn't there or responding;
            IsConnected = false;    // But make a note.
        }
    }

    /// <summary>
    /// Returns the most recently retrieved Pan value, refreshing it if it has not already been checked.
    /// </summary>
    /// <param name="refresh"></param>
    /// <returns>The pan value as a hex code in string format</returns>
    public string GetPanValue(bool? refresh = false)
    {
        _logger.Debug("GetPanValue(refresh={0})", refresh);
        try
        {
            if (_panValue == string.Empty || refresh == true)
            {
                // It might take more than one attempt. Give it a few...
                _panValue = string.Empty;
                int retries = 0;
                while (_panValue == string.Empty && retries < 5)
                {
                    retries++;
                    _ = GetPanTiltInfo();
                    if (_panValue == string.Empty)
                    {
                        _logger.Debug("Retrying GetPanValue(). Retry iteration={0}", retries);
                        Thread.Sleep(_receiveTimeout + 1);  // Short delay before retrying
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped GetPanValue()");
            throw;
        }
        return _panValue;
    }

    /// <summary>
    /// Returns the most recently retrieved Tilt value, refreshing it if it has not already been checked.
    /// </summary>
    /// <param name="refresh"></param>
    /// <returns>The tilt value as a hex code in string format</returns>
    public string GetTiltValue(bool? refresh = false)
    {
        _logger.Debug("GetTiltValue(refresh={0})", refresh);
        try
        {
            if (_tiltValue == string.Empty || refresh == true)
            {
                // It might take more than one attempt. Give it a few...
                _tiltValue = string.Empty;
                int retries = 0;
                while (_tiltValue == string.Empty && retries < 5)
                {
                    retries++;
                    _ = GetPanTiltInfo();
                    if (_tiltValue == string.Empty)
                    {
                        _logger.Debug("Retrying GetTiltValue(). Retry iteration={0}", retries);
                        Thread.Sleep(_receiveTimeout + 1);  // Very short delay before retrying
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped GetTiltValue()");
            throw;
        }
        return _tiltValue;
    }

    /// <summary>
    /// Returns the most recently retrieved Zoom value, refreshing it if it has not already been checked.
    /// </summary>
    /// <param name="refresh"></param>
    /// <returns>The zoom value as a hex code in string format</returns>
    public string GetZoomValue(bool? refresh = false)
    {
        _logger.Debug("GetZoomValue(refresh={0})", refresh);
        try
        {
            if (_zoomValue == string.Empty || refresh == true)
            {
                // It might take more than one attempt. Give it a few...
                _zoomValue = string.Empty;
                int retries = 0;
                while (_zoomValue == string.Empty && retries < 5)
                {
                    retries++;
                    _ = GetZoomInfo();
                    if (_zoomValue == string.Empty)
                    {
                        _logger.Debug("Retrying GetZoomValue(). Retry iteration={0}", retries);
                        Thread.Sleep(_receiveTimeout + 1);  // Very short delay before retrying
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped GetZoomValue()");
            throw;
        }
        return _zoomValue;
    }

    /// <summary>
    /// Asks the camera for its current pan and tilt values using the VISCA API
    /// </summary>
    /// <returns>A string describing the pan and tilt values. Also refreshes internal property values which 
    /// can be retrieved using GetPanValue() and GetTiltValue().</returns>
    public string GetPanTiltInfo()
    {
        string result;
        _logger.Debug("GetPanTiltInfo()");

        try
        {
            // Queue up the command and send it...
            byte[] command = [0x81, 0x09, 0x06, 0x12, 0xff];
            byte[] answer = SendViscaCommandSingleResponse(command);

            if (answer.Length >= 10)
            {
                string positionBytes = Convert.ToHexString(answer, 2, 8).ToString();

                _panValue = positionBytes[1].ToString()    // Take the least significant hex digits only.
                          + positionBytes[3].ToString()
                          + positionBytes[5].ToString()
                          + positionBytes[7].ToString();

                _tiltValue = positionBytes[9].ToString()
                           + positionBytes[11].ToString()
                           + positionBytes[13].ToString()
                           + positionBytes[15].ToString();

                result = "Pan =" + _panValue
                       + Environment.NewLine
                       + "Tilt=" + _tiltValue;

                _logger.Information("PtzCameraController ({0}) Pan={1} / Tilt={2}", IpAddress.ToString(), _panValue, _tiltValue);
            }
            else
            {
                _panValue = string.Empty;
                _tiltValue = string.Empty;
                result = "Pan/Tilt (Raw)=" + Convert.ToHexString(answer);

                _logger.Warning("GetPanTiltInfo() failed. Answer={0}", Convert.ToHexString(answer));

            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped GetPanTiltInfo()");
            throw;
        }

        return result;
    }

    /// <summary>
    /// Directly sets the pan and tilt values for the PTZ Camera using the VISCA API
    /// </summary>
    /// <param name="pan">Pan position as 4 hex digits (in string form)</param>
    /// <param name="tilt">Tilt position as 4 hex digits (in string form)</param>
    /// <param name="panSpeedPct">Pan speed as a percentage from slowest to fastest</param>
    /// <param name="tiltSpeedPct">Tilt speed as a percentage from slowest to fastest</param>
    public bool SetPanTilt(string pan, string tilt, double panSpeedPct = 0.5, double tiltSpeedPct = 0.5)
    {
        bool result = false;
        _logger.Debug("SetPanTilt(pan={0}, tilt={1}, panSpeedPct={2}, tiltSpeedPct={3})", pan, tilt, panSpeedPct, tiltSpeedPct);
        try
        {
            // Make sure the pan and tilt speeds are between 0.0 and 1.0...
            panSpeedPct = Math.Min(Math.Max(panSpeedPct, 0), 1);
            tiltSpeedPct = Math.Min(Math.Max(tiltSpeedPct, 0), 1);

            // Make sure the pan and tilt positions are 4 hex digits each
            pan = pan.Trim();
            tilt = tilt.Trim();

            if (pan.Length != 4 || tilt.Length != 4)
            {
                throw new ArgumentException("Pan and Tilt arguments must be 4 hex digits each");
            }
            if (!UInt32.TryParse(pan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException("Pan argument must contain only valid hex digits", nameof(pan));
            }
            if (!UInt32.TryParse(tilt, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException("Tilt argument must contain only valid hex digits", nameof(tilt));
            }

            // Work out the pan/tilt speeds as bytes instead of percentages
            const byte minSpeed = 0x01;
            const byte maxPanSpeed = 0x18;
            const byte maxTiltSpeed = 0x14;

            byte panSpeedHex = PercentToHexByte(panSpeedPct, minSpeed, maxPanSpeed);
            byte tiltSpeedHex = PercentToHexByte(tiltSpeedPct, minSpeed, maxTiltSpeed);

            //Sent up and send the set pan/tilt command to the camera
            //-------------------------------------------------------
            //                                        vpan  vtlt  ----pan-posn----------  ----tilt-posn---------
            byte[] command = [0x81, 0x01, 0x06, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff];
            // Set the pan speed
            command[4] = panSpeedHex;
            // Set the tilt speed
            command[5] = tiltSpeedHex;
            // Set the pan position
            command[6] = Convert.ToByte(pan[0].ToString(), 16);
            command[7] = Convert.ToByte(pan[1].ToString(), 16);
            command[8] = Convert.ToByte(pan[2].ToString(), 16);
            command[9] = Convert.ToByte(pan[3].ToString(), 16);
            // Set the tilt position
            command[10] = Convert.ToByte(tilt[0].ToString(), 16);
            command[11] = Convert.ToByte(tilt[1].ToString(), 16);
            command[12] = Convert.ToByte(tilt[2].ToString(), 16);
            command[13] = Convert.ToByte(tilt[3].ToString(), 16);

            // Send the VISCA command without regard to any return code (none will be forthcoming)
            SendViscaCommandWithoutResponse(command);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped SetPanTilt()");
            throw;
        }
        return result;
    }

    /// <summary>
    /// Directly sets the zoom of the camera using the VISCA API
    /// </summary>
    /// <param name="zoom">Zoom position as 4 hex digits (in string form)</param>
    public bool SetZoom(string zoom)
    {
        _logger.Debug("SetZoom(zoom={0})", zoom);
        bool result = false;
        try
        {
            // Make sure the pan and tilt positions are 4 hex digits each
            zoom = zoom.Trim();

            if (zoom.Length != 4)
            {
                throw new ArgumentException("Zoom argument must be 4 hex digits", nameof(zoom));
            }
            if (!UInt32.TryParse(zoom, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException("Zoom argument must contain only valid hex digits", nameof(zoom));
            }

            // Set up and send the set zoom command to the camera
            //                                        ----zoom-posn---------  
            byte[] command = [0x81, 0x01, 0x04, 0x47, 0x00, 0x00, 0x00, 0x00, 0xff];
            // Set the zoom position
            command[4] = Convert.ToByte(zoom[0].ToString(), 16);
            command[5] = Convert.ToByte(zoom[1].ToString(), 16);
            command[6] = Convert.ToByte(zoom[2].ToString(), 16);
            command[7] = Convert.ToByte(zoom[3].ToString(), 16);

            // Send the VISCA command without regard to any return code (none will be forthcoming)
            SendViscaCommandWithoutResponse(command);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped SetZoom()");
            throw;
        }
        return result;
    }

    /// <summary>
    /// Convert a speed (pan/tilt) as a percentage to a hex (byte) value for use in a VISCA command
    /// </summary>
    /// <param name="percent">Value from 0.0 (slowest) to 1.1 (fastest)</param>
    /// <param name="minByte">Slowest hex value</param>
    /// <param name="maxByte">Fastest hex value</param>
    /// <returns>The speed as a number that can be expressed as a byte for VISCA commands</returns>
    private static byte PercentToHexByte(double percent, byte minByte, byte maxByte)
    {
        // Sanity check the percent argument
        percent = Math.Min(Math.Max(percent, 0), 1);
        // Calculate the result
        byte result = (byte)((int)Math.Floor(percent * ((int)maxByte - (int)minByte)) + (int)minByte);
        return result;
    }

    /// <summary>
    /// Retrieves the current camera's Zoom position using the VISCA API
    /// </summary>
    /// <returns>Zoom position as 4 hex digits (in string form)</returns>
    public string GetZoomInfo()
    {
        string result;
        _logger.Debug("GetZoomInfo()");

        try
        {
            // Queue up the command and send it...
            byte[] command = [0x81, 0x09, 0x04, 0x47, 0xff];
            byte[] answer = SendViscaCommandSingleResponse(command);

            if (answer.Length >= 6)
            {
                string positionBytes = Convert.ToHexString(answer, 2, 4).ToString();
                _zoomValue = positionBytes[1].ToString()    // Take the least significant hex digits only.
                           + positionBytes[3].ToString()
                           + positionBytes[5].ToString()
                           + positionBytes[7].ToString();

                result = "Zoom=" + _zoomValue;
            }
            else
            {
                _zoomValue = string.Empty;
                result = "Zoom (Raw)=" + Convert.ToHexString(answer);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped GetZoomInfo()");
            throw;
        }
        return result;
    }

    /////// <summary>
    /////// Pauses execution while the camera is in motion
    /////// </summary>
    /////// <param name="timeOutSeconds">Number of seconds after which to give up</param>
    ////public void WaitForCameraMotion(double timeOutSeconds = 10.0)
    ////{
    ////    _logger.Debug("=>IsMoving()");
    ////    DateTime timeout = DateTime.Now.AddSeconds(timeOutSeconds);
    ////    while (this.IsMoving() && DateTime.Now < timeout)
    ////    {
    ////        Thread.Sleep(_receiveTimeout + 1);
    ////    }
    ////    _logger.Debug("<=IsMoving()");
    ////}

    ////public bool IsMoving()
    ////{
    ////    bool result = false;
    ////    _logger.Verbose("IsMoving()");

    ////    // Note the current values, seeding them as necessary
    ////    string previousPan = GetPanValue();
    ////    string previousTilt = GetTiltValue();
    ////    string previousZoom = GetZoomValue();

    ////    // Wait a very short period of time.
    ////    Thread.Sleep(_receiveTimeout + 1);

    ////    // See if they've changed. 
    ////    string currentPan = GetPanValue(true);
    ////    string currentTilt = GetTiltValue();    // Refreshing pan also refreshes tilt.
    ////    string currentZoom = GetZoomValue(true);

    ////    // If they're not all steady then we're moving
    ////    if (!(currentPan.Equals(previousPan)
    ////     && currentTilt.Equals(previousTilt)
    ////     && currentZoom.Equals(previousZoom)))
    ////    {
    ////        result = true;
    ////    }
    ////    _logger.Verbose("PTZ Before: {0} {1} {2}", previousPan, previousTilt, previousZoom);
    ////    _logger.Verbose("PTZ After:  {0} {1} {2}", currentPan, currentTilt, currentZoom);

    ////    _logger.Verbose("IsMoving() = {0}", result);

    ////    return result;
    ////}

    /// <summary>
    /// Recalls a stored preset for the PTZ Camera using the VISCA API
    /// </summary>
    /// <param name="preset">Preset number (0-9)</param>
    /// <returns>True if the command is accepted, false otherwise</returns>
    public bool RecallPreset(int preset)
    {
        bool result = false;
        _logger.Debug("RecallPreset(preset={0})", preset);

        try
        {
            // Queue up the command and send it...
            byte[] command = [0x81, 0x01, 0x04, 0x3f, 0x02, 0x00, 0xff];
            command[5] = (byte)preset;

            byte[] answer = SendViscaCommandWaitForCompletion(command);

            if (answer.Length >= 2)
            {
                string code = Convert.ToHexString(answer, 0, 2).ToString();
                if (code.StartsWith("904"))
                {
                    result = true;
                }
            }

            // Assume that recalling a preset invalidates the pan/tilt/zoom values
            _panValue = string.Empty;
            _tiltValue = string.Empty;
            _zoomValue = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped RecallPreset()");
            throw;
        }
        // Sanity check the preset number parameter
        if (preset < 0 || preset > 9)
        {
            return result;
        }
        return result;
    }

    /// <summary>
    /// Sends a VISCA command to the PTZ Camera at the IpAddress and Port without retrieving a response
    /// </summary>
    /// <param name="command">The command bytes to send to the camera</param>
    private void SendViscaCommandWithoutResponse(byte[] command)
    {
        byte[] response = new byte[4096];
        byte[] answer = [];
        int responseAttempts = 0;

        _logger.Debug("SendViscaCommandWithoutResponse(command={0})", command);
        try
        {
            var bytesSent = _sender!.Send(command, SocketFlags.None);
            // We don't care about the response but check for one in case one is forthcoming so that 
            // the works don't get gummed up.
            while (responseAttempts < 100)
            {
                responseAttempts++;
                var bytesReceived = _sender!.Receive(response, SocketFlags.None);
                if (bytesReceived == 0)
                {
                    // There is nothing left to retrieve
                    _logger.Debug("SendViscaCommandWithoutResponse Response is Empty. Attempt={1}", answer, responseAttempts);
                    break;
                }
                else
                {
                    // Trim the response down to the end of file marker (0xFF)
                    // Find the 0xFF byte
                    int eof = FindEofMarker(response);
                    if (eof > -1)
                    {
                        answer = new byte[eof];
                        Buffer.BlockCopy(response, 0, answer, 0, eof);
                    }
                    _logger.Debug("SendViscaCommandWithoutResponse Response=[{0}] Attempt={1}", answer, responseAttempts);

                    // Give it a bit of time and keep trying
                    Thread.Sleep(100);
                }
            }
        }
        catch (SocketException socketEx)
        {
            // Probably a receive timeout, which is not a big deal.
            if (socketEx.Message.Contains("timeout"))
            {
                _logger.Debug(socketEx, "Timeout error trapped in SendViscaCommandWithoutResponse()");
            }
            else
            {
                _logger.Error(socketEx, "Socket error trapped in SendViscaCommandWithoutResponse()");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped in SendViscaCommandWithoutResponse()");
            throw;
        }
    }

    /// <summary>
    /// Sends a VISCA command to the PTZ Camera at the IpAddress and Port 
    /// retrieving a singular response before returning
    /// </summary>
    /// <param name="command">The command bytes to send to the camera</param>
    /// <returns>A Byte array containing the full response</returns>
    private byte[] SendViscaCommandSingleResponse(byte[] command)
    {
        byte[] response = new byte[4096];
        byte[] answer = [];
        byte[] completeAnswer = [];
        int responseAttempts = 0;

        _logger.Debug("SendViscaCommandSingleResponse(command={0})", command);
        try
        {
            var bytesSent = _sender!.Send(command, SocketFlags.None);
            // We don't care about the response but check for one in case one is forthcoming so that 
            // the works don't get gummed up.
            while (responseAttempts < 100)
            {
                responseAttempts++;
                var bytesReceived = _sender!.Receive(response, SocketFlags.None);
                if (bytesReceived == 0)
                {
                    // There is nothing left to retrieve
                    _logger.Debug("SendViscaCommandSingleResponse Response is Empty. Attempt={1}", answer, responseAttempts);
                    break;
                }
                else
                {
                    // Trim the response down to the end of file marker (0xFF)
                    // Find the 0xFF byte
                    int eof = FindEofMarker(response);
                    if (eof > -1)
                    {
                        // Get this part of the answer
                        answer = new byte[eof];
                        Buffer.BlockCopy(response, 0, answer, 0, eof);

                        int newTotalLength = answer.Length + completeAnswer.Length;
                        // Get a copy of the previous part(s) of the answer
                        byte[] soFar = new byte[completeAnswer.Length];
                        Buffer.BlockCopy(completeAnswer, 0, soFar, 0, completeAnswer.Length);
                        completeAnswer = new byte[newTotalLength];
                        // Put the two parts together...
                        Buffer.BlockCopy(soFar, 0, completeAnswer, 0, soFar.Length);
                        Buffer.BlockCopy(answer, 0, completeAnswer, soFar.Length, answer.Length);
                    }
                    _logger.Debug("SendViscaCommandSingleResponse Response (so far)=[{0}] Attempt={1}", completeAnswer, responseAttempts);

                    // Give it a bit of time and keep trying
                    Thread.Sleep(100);
                }
            }
        }
        catch (SocketException socketEx)
        {
            // Probably a receive timeout, which is not a big deal.
            if (socketEx.Message.Contains("timeout"))
            {
                _logger.Debug(socketEx, "Timeout error trapped in SendViscaCommandSingleResponse()");
            }
            else
            {
                _logger.Error(socketEx, "Socket error trapped in SendViscaCommandSingleResponse()");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped in SendViscaCommandSingleResponse()");
            throw;
        }

        return completeAnswer;
    }


    /// <summary>
    /// Sends a VISCA command to the PTZ Camera at the IpAddress and Port 
    /// retrieving responses and waiting for a "command completed" response 
    /// before returning
    /// </summary>
    /// <param name="command">The command bytes to send to the camera</param>
    /// <returns>A Byte array containing the full response</returns>
    private byte[] SendViscaCommandWaitForCompletion(byte[] command)
    {
        byte[] response = new byte[4096];
        byte[] answer = [];
        byte[] completeAnswer = [];
        int responseAttempts = 0;
        string prefix = string.Empty;
        bool commandCompleted = false;

        _logger.Debug("SendViscaCommandWaitForCompletion(command={0})", command);
        try
        {
            var bytesSent = _sender!.Send(command, SocketFlags.None);
            // We don't care about the response but check for one in case one is forthcoming so that 
            // the works don't get gummed up.
            while (responseAttempts < 100 && commandCompleted == false)
            {
                responseAttempts++;
                var bytesReceived = _sender!.Receive(response, SocketFlags.None);
                if (bytesReceived == 0)
                {
                    // There is nothing to retrieve for the moment.
                    _logger.Debug("SendViscaCommandWaitForCompletion Response is Empty. Attempt={1}", answer, responseAttempts);
                    // Reset any prior response since what matters is the command completed response
                    completeAnswer = [];
                }
                else
                {
                    // Trim the response down to the end of file marker (0xFF)
                    // Find the 0xFF byte
                    int eof = FindEofMarker(response);
                    if (eof > -1)
                    {
                        // Get this part of the answer
                        answer = new byte[eof];
                        Buffer.BlockCopy(response, 0, answer, 0, eof);

                        int newTotalLength = answer.Length + completeAnswer.Length;
                        // Get a copy of the previous part(s) of the answer
                        byte[] soFar = new byte[completeAnswer.Length];
                        Buffer.BlockCopy(completeAnswer, 0, soFar, 0, completeAnswer.Length);
                        completeAnswer = new byte[newTotalLength];
                        // Put the two parts together...
                        Buffer.BlockCopy(soFar, 0, completeAnswer, 0, soFar.Length);
                        Buffer.BlockCopy(answer, 0, completeAnswer, soFar.Length, answer.Length);

                        prefix = Convert.ToHexString(completeAnswer, 0, 2).ToString();
                        if (prefix.StartsWith("904"))
                        {
                            // Command accepted
                            _logger.Debug("PTZ Optics VISCA command accepted.");
                        }
                        else if (prefix.StartsWith("905"))
                        {
                            // Command completed
                            _logger.Debug("PTZ Optics VISCA command completed.");
                            commandCompleted = true;
                        }
                        else if (prefix.StartsWith("906"))
                        {
                            // Some kind of problem with the command
                            _logger.Warning("PTZ Optics VISCA command failed. Return code={0}", prefix);
                        }
                        else
                        {
                            _logger.Debug("Unknown PTZ Optics VISCA command return value.");
                        }
                    }
                    _logger.Debug("SendViscaCommandWaitForCompletion Response (so far)=[{0}] Attempt={1}", completeAnswer, responseAttempts);

                    // Give it a bit of time and keep trying
                    Thread.Sleep(100);
                }
            }
        }
        catch (SocketException socketEx)
        {
            // Probably a receive timeout, which is not a big deal.
            if (socketEx.Message.Contains("timeout"))
            {
                _logger.Debug(socketEx, "Timeout error trapped in SendViscaCommandWaitForCompletion()");
            }
            else
            {
                _logger.Error(socketEx, "Socket error trapped in SendViscaCommandWaitForCompletion()");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error trapped in SendViscaCommandWaitForCompletion()");
            throw;
        }

        return completeAnswer;
    }



    /////// <summary>
    /////// Sends a VISCA command to the PTZ Camera at the IpAddress and Port
    /////// </summary>
    /////// <param name="command">VISCA Command as an array of bytes to be sent to the camera</param>
    /////// <param name="awaitResponse">Use True if a response is expected, False if response is not sought</param>
    /////// <returns>The last return from the command via the API. If awaitResponse = false, this will be an empty array</returns>
    ////private byte[] SendViscaCommand(byte[] command, ResultAction resultAction)
    ////{
    ////    byte[] response = new byte[4096];
    ////    byte[] answer = [];
    ////    string prefix = string.Empty;
    ////    int responseAttempts = 0;

    ////    _logger.Debug("SendViscaCommandAsync(command={0}, ResultAction={1})", command, resultAction);

    ////    try
    ////    {
    ////        var bytesSent = _sender!.Send(command);
    ////        if (resultAction != ResultAction.None)
    ////        {
    ////            while ((resultAction == ResultAction.Single && responseAttempts == 1)
    ////                || (resultAction == ResultAction.WaitForSuccess && responseAttempts < 100))
    ////            {
    ////                responseAttempts++;

    ////                var bytesReceived = _sender.Receive(response);

    ////                // Trim the response down to the end of file marker (0xFF)
    ////                // Find the 0xFF byte
    ////                int eof = FindEofMarker(response);
    ////                if (eof > -1)
    ////                {
    ////                    answer = new byte[eof];
    ////                    Buffer.BlockCopy(response, 0, answer, 0, eof);
    ////                    prefix = Convert.ToHexString(answer, 0, 2).ToString();
    ////                }
    ////                else
    ////                {
    ////                    // Send the whole mess.
    ////                    answer = response;
    ////                }
    ////                _logger.Debug("SendViscaCommandAsync Response=[{0}] attempt={1}", answer, responseAttempts);

    ////                if (prefix.StartsWith("904"))
    ////                {
    ////                    // Command accepted
    ////                    _logger.Debug("PTZ Optics VISCA command accepted.");
    ////                }
    ////                else if (prefix.StartsWith("905"))
    ////                {
    ////                    // Command completed
    ////                    _logger.Debug("PTZ Optics VISCA command completed.");

    ////                    if (resultAction == ResultAction.WaitForSuccess)
    ////                    {
    ////                        // We got the answer finally
    ////                        break;
    ////                    }
    ////                }
    ////                else if (prefix.StartsWith("906"))
    ////                {
    ////                    // Some kind of problem with the command
    ////                    _logger.Warning("PTZ Optics VISCA command failed. Return code={0}", prefix);
    ////                }
    ////                else
    ////                {
    ////                    _logger.Debug("Unknown PTZ Optics VISCA command return value.");
    ////                }
    ////                if (resultAction == ResultAction.WaitForSuccess)
    ////                {
    ////                    Thread.Sleep(100);  // Short pause for the success result
    ////                }
    ////            }
    ////        }
    ////    }
    ////    catch (SocketException socketEx)
    ////    {
    ////        // Probably a receive timeout, which is not a big deal.
    ////        if (socketEx.Message.Contains("timeout"))
    ////        {
    ////            _logger.Debug(socketEx, "Timeout error trapped SendViscaCommandAsync()");
    ////        }
    ////        else
    ////        {
    ////            _logger.Error(socketEx, "Socket error trapped SendViscaCommandAsync()");
    ////        }
    ////    }
    ////    catch (Exception ex)
    ////    {
    ////        _logger.Error(ex, "Error trapped SendViscaCommandAsync()");
    ////        throw;
    ////    }
    ////    return answer;
    ////}

    /// <summary>
    /// Runs through a byte array looking for 0xff. Not sure why Array.IndexOf() isn't working, but it is what it is.
    /// </summary>
    /// <param name="array"></param>
    /// <returns></returns>
    private static int FindEofMarker(byte[] array)
    {
        int position = -1;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == 255)
            {
                position = i;
                break;

            }
        }
        return position;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
                if (_sender != null)
                {
                    try
                    {
                        _sender.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception)
                    {
                        // Swallow it.
                    }
                    finally
                    {
                        _sender.Close();
                        _sender.Dispose();
                    }
                }
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            disposedValue = true;
        }
    }

    // // Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~PtzCameraController()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

////public enum ResultAction
////{
////    None,
////    Single,
////    WaitForSuccess
////}
