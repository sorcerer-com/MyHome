using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

using Mictlanix.DotNet.Onvif;
using Mictlanix.DotNet.Onvif.Device;
using Mictlanix.DotNet.Onvif.Imaging;
using Mictlanix.DotNet.Onvif.Media;
using Mictlanix.DotNet.Onvif.Ptz;

using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

using OnvifEvents;

using OpenCvSharp;

namespace MyHome.Systems.Devices.Sensors
{
    public class Camera : BaseSensor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public enum Movement
        {
            UP,
            DOWN,
            RIGHT,
            LEFT,
            ZOOMIN,
            ZOOMOUT
        }

        [UiProperty(true)]
        public bool IsOnvifSupported { get; set; }

        [UiProperty(true, "seconds")]
        public int ReadDataInterval { get; set; } // seconds

        [JsonIgnore]
        public bool IsOpened => this.Capture.IsOpened();


        private VideoCapture capture;
        private VideoCapture Capture
        {
            get
            {
                if (this.capture == null)
                {
                    this.capture = new VideoCapture();
                    this.capture.Set(CaptureProperty.BufferSize, 1);
                }

                // if capture isn't opened try again but only if the previous try was at least 1 minute ago
                if (!this.capture.IsOpened() && DateTime.Now - this.lastUse > TimeSpan.FromMinutes(1))
                {
                    logger.Info($"Opening camera: {this.Name} ({this.Room.Name})");
                    if (int.TryParse(this.Address, out int device))
                        this.capture.Open(device);
                    else
                        this.capture.Open(this.GetStreamAddress());
                    this.lastUse = DateTime.Now;
                }
                if (this.capture.IsOpened())
                    this.lastUse = DateTime.Now;

                return this.capture;
            }
        }

        private DateTime lastUse;
        private DateTime nextDataRead;

        private readonly Dictionary<Type, object> onvif;


        public Camera()
        {
            this.IsOnvifSupported = true;
            this.ReadDataInterval = 15;

            this.capture = null;
            this.lastUse = DateTime.Now.AddMinutes(-1);
            this.nextDataRead = DateTime.Now.AddMinutes(1); // start reading 1 minute after start

            this.onvif = new Dictionary<Type, object>();

            Task.Run(() => { lock (this.Capture) this.Capture.Release(); }); // prepare capture for opening
        }

        public override void Update()
        {
            base.Update();

            // release the capture if it isn't used for more then 5 minutes
            if (this.capture != null && this.capture.IsOpened() && DateTime.Now - this.lastUse > TimeSpan.FromMinutes(5))
            {
                logger.Info($"Release camera: {this.Name} ({this.Room.Name})");
                this.capture.Release();
            }

            // read sensor data
            if (this.IsOnvifSupported && DateTime.Now > this.nextDataRead)
            {
                if (this.ReadData(DateTime.Now))
                    this.nextDataRead = DateTime.Now.AddSeconds(this.ReadDataInterval);
                else // if doesn't succeed wait more before next try
                    this.nextDataRead = DateTime.Now.AddMinutes(this.ReadDataInterval * 4);
            }
        }


        public Mat GetImage(bool initIfEmpty = true, Size? size = null, bool timestamp = true)
        {
            lock (this.Capture)
            {
                var image = new Mat();
                this.Capture.Read(image);
                if (image.Empty())
                {
                    this.Capture.Release(); // try to release the camera and open it again next time
                    this.lastUse = DateTime.Now.AddMinutes(-1);
                    if (!initIfEmpty)
                        return null;
                    image = new Mat(480, 640, MatType.CV_8UC3, 0);
                    image.Line(0, 0, 640, 480, Scalar.Red, 2, LineTypes.AntiAlias);
                    image.Line(640, 0, 0, 480, Scalar.Red, 2, LineTypes.AntiAlias);
                }
                if (size.HasValue)
                    image.Resize(size.Value);
                if (timestamp)
                {
                    var scale = image.Size(0) / 800.0;
                    var text = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scale, (int)Math.Round(scale * 3), out int _);
                    image.PutText(text, new Point(5, 5 + textSize.Height), HersheyFonts.HersheySimplex, scale,
                        Scalar.White, (int)Math.Round(scale * 3), LineTypes.AntiAlias);
                }
                return image;
            }
        }

        public bool SaveImage(string filepath, bool initIfEmpty = true, Size? size = null, bool timestamp = true)
        {
            logger.Debug($"Camera '{this.Name}' ({this.Room.Name}) save image: {filepath}");

            var image = this.GetImage(initIfEmpty, size, timestamp);
            return Cv2.ImWrite(filepath, image);
        }

        public void Move(Movement movement)
        {
            try
            {
                if (!this.IsOnvifSupported)
                    return;

                logger.Debug($"Camera '{this.Name}' ({this.Room.Name}) move: {movement}");

                var speed = new Mictlanix.DotNet.Onvif.Common.PTZSpeed
                {
                    PanTilt = new Mictlanix.DotNet.Onvif.Common.Vector2D(),
                    Zoom = new Mictlanix.DotNet.Onvif.Common.Vector1D()
                };
                var options = this.GetOnvif<Mictlanix.DotNet.Onvif.Common.PTZConfigurationOptions>();
                switch (movement)
                {
                    case Movement.UP:
                        speed.PanTilt.y = options.Spaces.RelativePanTiltTranslationSpace[0].YRange.Max;
                        break;
                    case Movement.DOWN:
                        speed.PanTilt.y = options.Spaces.RelativePanTiltTranslationSpace[0].YRange.Min;
                        break;
                    case Movement.LEFT:
                        speed.PanTilt.x = options.Spaces.RelativePanTiltTranslationSpace[0].XRange.Max;
                        break;
                    case Movement.RIGHT:
                        speed.PanTilt.x = options.Spaces.RelativePanTiltTranslationSpace[0].XRange.Min;
                        break;
                    case Movement.ZOOMIN:
                        speed.Zoom.x = options.Spaces.ZoomSpeedSpace[0].XRange.Max;
                        break;
                    case Movement.ZOOMOUT:
                        speed.Zoom.x = options.Spaces.ZoomSpeedSpace[0].XRange.Min;
                        break;
                }
                var token = this.GetOnvif<Mictlanix.DotNet.Onvif.Common.Profile>().token;
                this.GetOnvif<PTZClient>().ContinuousMoveAsync(token, speed, "PT1S"); // move for 1 second
            }
            catch (Exception e)
            {
                logger.Error("Cannot move camera");
                logger.Debug(e);
            }
        }

        public bool Restart()
        {
            try
            {
                if (!this.IsOnvifSupported)
                    return false;

                logger.Debug($"Restart camera '{this.Name}' ({this.Room.Name})");

                var device = this.GetOnvif<DeviceClient>();
                device.SystemRebootAsync().Wait();
                return true;
            }
            catch (Exception e)
            {
                logger.Error("Cannot restart camera");
                logger.Debug(e);
            }
            return false;
        }


        private string GetStreamAddress()
        {
            // rtsp://192.168.0.120:554/user=admin_password=12345_channel=1_stream=0.sdp?real_stream
            if (this.Address.StartsWith("rtsp://") || !this.IsOnvifSupported)
                return this.Address;

            if (!this.IsOnvifSupported)
                return null;

            try
            {
                var media = this.GetOnvif<MediaClient>();
                var streamSetup = new Mictlanix.DotNet.Onvif.Common.StreamSetup
                {
                    Stream = Mictlanix.DotNet.Onvif.Common.StreamType.RTPUnicast,
                    Transport = new Mictlanix.DotNet.Onvif.Common.Transport
                    {
                        Protocol = Mictlanix.DotNet.Onvif.Common.TransportProtocol.RTSP
                    }
                };
                var token = this.GetOnvif<Mictlanix.DotNet.Onvif.Common.Profile>().token;
                var response = media.GetStreamUriAsync(streamSetup, token).Result.Uri;
                // replace internal IP address with the real one
                var responseIp = response[7..response.IndexOf(":", 7)];
                var ip = this.Address.Split(new char[] { ':', '@' })[2];
                return response.Replace(responseIp, ip);
            }
            catch (Exception e)
            {
                logger.Error("Cannot get stream address");
                logger.Debug(e);
            }
            return null;
        }

        private T GetOnvif<T>() where T : class
        {
            if (!this.IsOnvifSupported)
                return null;

            lock (this.onvif)
            {
                if (!this.onvif.ContainsKey(typeof(T)))
                {
                    logger.Trace($"Creating Onvif {typeof(T).Name}");
                    var address = this.Address.Split('@')[1]; // username:password@ip:port #8899
                    var username = this.Address.Split('@')[0].Split(':')[0];
                    var password = this.Address.Split('@')[0].Split(':')[1];
                    if (typeof(T) == typeof(DeviceClient))
                    {
                        this.onvif.Add(typeof(T), OnvifClientFactory.CreateDeviceClientAsync(address, username, password).Result);
                    }
                    else if (typeof(T) == typeof(ImagingClient))
                    {
                        this.onvif.Add(typeof(T), OnvifClientFactory.CreateImagingClientAsync(address, username, password).Result);
                    }
                    else if (typeof(T) == typeof(MediaClient))
                    {
                        this.onvif.Add(typeof(T), OnvifClientFactory.CreateMediaClientAsync(address, username, password).Result);
                    }
                    else if (typeof(T) == typeof(PTZClient))
                    {
                        this.onvif.Add(typeof(T), OnvifClientFactory.CreatePTZClientAsync(address, username, password).Result);
                    }
                    else if (typeof(T) == typeof(Mictlanix.DotNet.Onvif.Common.Profile))
                    {
                        this.onvif.Add(typeof(T), this.GetOnvif<MediaClient>().GetProfilesAsync().Result.Profiles[0]);
                    }
                    else if (typeof(T) == typeof(Mictlanix.DotNet.Onvif.Common.PTZConfigurationOptions))
                    {
                        var token = this.GetOnvif<Mictlanix.DotNet.Onvif.Common.Profile>().token;
                        this.onvif.Add(typeof(T), this.GetOnvif<PTZClient>().GetConfigurationOptionsAsync(token).Result);
                    }
                    else if (typeof(T) == typeof(PullPointSubscriptionClient))
                    {
                        this.onvif.Add(typeof(T), OnvifEventsHelper.CreateEventsAsync(this.GetOnvif<DeviceClient>(), username, password).Result);
                    }
                }
            }
            return (T)this.onvif[typeof(T)];
        }

        protected override JToken ReadDataInternal()
        {
            if (!this.IsOnvifSupported)
            {
                logger.Debug("Not Onfiv camera, so no data to read");
                return new JArray();
            }

            try
            {
                var pullPointClient = this.GetOnvif<PullPointSubscriptionClient>();
                var request = new PullMessagesRequest("PT5S", 100, null);
                var response = pullPointClient.PullMessagesAsync(request).Result;

                var data = response.NotificationMessage[0].Message.ChildNodes[3];
                var result = new JArray();
                foreach (XmlNode child in data.ChildNodes)
                {
                    if (child == null || child.Attributes == null)
                        continue;

                    var value = child.Attributes["Value"].Value.ToLower();
                    var item = new JObject
                    {
                        ["name"] = child.Attributes["Name"].Value,
                        ["value"] = Utils.Utils.ParseValue(value, null)
                    };
                    result.Add(item);
                }
                return result;
            }
            catch (Exception e)
            {
                logger.Error("Cannot read data from camera");
                logger.Debug(e);
                return null;
            }
        }
    }
}
