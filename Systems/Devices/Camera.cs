using System;
using System.Collections.Generic;

using Mictlanix.DotNet.Onvif;
using Mictlanix.DotNet.Onvif.Device;
using Mictlanix.DotNet.Onvif.Imaging;
using Mictlanix.DotNet.Onvif.Media;
using Mictlanix.DotNet.Onvif.Ptz;

using MyHome.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

using OpenCvSharp;

namespace MyHome.Systems.Devices
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

        public bool IsOnvifSupported { get; set; }

        [JsonIgnore]
        public bool IsOpened => this.Capture.IsOpened();


        private VideoCapture capture;
        private VideoCapture Capture
        {
            get
            {
                if (this.capture == null)
                {
                    logger.Info($"Opening camera: {this.Name}");
                    var address = this.GetStreamAddress();
                    if (int.TryParse(address, out int device))
                        this.capture = new VideoCapture(device);
                    else
                        this.capture = new VideoCapture(address);
                    this.capture.Set(CaptureProperty.BufferSize, 1);
                }
                else if (!this.capture.IsOpened() && DateTime.Now - this.lastUse > TimeSpan.FromMinutes(1))
                {
                    // if capture isn't opened try again but only if the previous try was at least 1 minutes ago
                    logger.Info($"Opening camera: {this.Name}");
                    var address = this.GetStreamAddress();
                    if (int.TryParse(address, out int device))
                        this.capture = new VideoCapture(device);
                    else
                        this.capture = new VideoCapture(address);
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


        private Camera() : this(null, null, null, null) { } // for json deserialization

        public Camera(DevicesSystem owner, string name, Room room, string address) : base(owner, name, room, address)
        {
            this.IsOnvifSupported = true;

            this.capture = null;
            this.lastUse = DateTime.Now;
            this.nextDataRead = DateTime.Now;

            this.onvif = new Dictionary<Type, object>();
        }

        public override void Update()
        {
            base.Update();

            //release the capture if it isn't used for more then 5 minutes
            if (this.capture != null && this.capture.IsOpened() && DateTime.Now - this.lastUse > TimeSpan.FromMinutes(5))
            {
                logger.Info($"Release camera: {this.Name}");
                this.capture.Release();
            }

            // read sensor data every 15 seconds
            if (this.IsOnvifSupported && DateTime.Now > this.nextDataRead)
            {
                this.ReadData(DateTime.Now);
                this.nextDataRead = DateTime.Now.AddSeconds(15); // TODO: maybe extract as param, maybe often
            }
        }

        public Mat GetImage(Size? size = null, bool timestamp = true)
        {
            lock (this.Capture)
            {
                var image = new Mat();
                this.Capture.Read(image);
                if (image == null || image.Empty())
                {
                    this.Capture.Release(); // try to release the camera and open it again next time
                    this.lastUse = DateTime.Now.AddMinutes(-1);
                    image = new Mat(480, 640, MatType.CV_8UC3);
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

        // TODO: getImageData

        public bool SaveImage(string fileName, Size? size = null, bool timestamp = true)
        {
            logger.Debug($"Camera '{this.Name}' save image: {fileName}");

            var image = this.GetImage(size, timestamp);
            return Cv2.ImWrite(fileName, image);
        }

        public void Move(Movement movement)
        {
            try
            {
                logger.Debug($"Camera '{this.Name}' move: {movement}");

                if (!this.IsOnvifSupported)
                    return;

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
                logger.Error(e, "Cannot move camera");
            }
        }

        private string GetStreamAddress()
        {
            // rtsp://192.168.0.120:554/user=admin_password=12345_channel=1_stream=0.sdp?real_stream
            if (this.Address.StartsWith("rtsp://"))
                return this.Address;

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
                var ip = this.Address.Split(':', '@')[2];
                return response.Replace(responseIp, ip);
            }
            catch (Exception e)
            {
                logger.Error(e, "Cannot get stream address");
                return "0";
            }
        }

        private T GetOnvif<T>() where T : class
        {
            if (!this.IsOnvifSupported)
                return null;

            if (!this.onvif.ContainsKey(typeof(T)))
            {
                logger.Debug($"Creating Onvif {typeof(T).Name}");
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
            }
            return (T)this.onvif[typeof(T)];
        }

        protected override JToken ReadDataInternal()
        {
            if (!this.IsOnvifSupported)
            {
                logger.Debug("Not Onfiv camera, so no data to read");
                return null;
            }

            try
            {
                // TODO: no events in OnvifClient.Core, maybe add python tool (sensor) to get camera data
                // Can try to add event.wsdl as Service Reference and https://github.com/BogdanovKirill/OnvifEventsReceiver/tree/master
                var json = "[{\"name\": \"IsMotion\", \"value\": true}]";
                return JToken.Parse(json);
            }
            catch (Exception e)
            {
                logger.Error(e, "Cannot read data from camera");
                return null;
            }
        }
    }
}
