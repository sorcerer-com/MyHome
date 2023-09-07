using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Mictlanix.DotNet.Onvif;
using Mictlanix.DotNet.Onvif.Device;
using Mictlanix.DotNet.Onvif.Imaging;
using Mictlanix.DotNet.Onvif.Media;
using Mictlanix.DotNet.Onvif.Ptz;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

using OnvifEvents;

using OpenCvSharp;

namespace MyHome.Systems.Devices.Sensors
{
    public class Camera : BaseSensor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private const int ArchiveImagesCount = 300;

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
        public string Address { get; set; }

        [UiProperty(true)]
        public bool IsOnvifSupported { get; set; }

        [UiProperty(true, "seconds")]
        public int ReadDataInterval { get; set; } // seconds

        [UiProperty(true)]
        public bool Record { get; set; }

        [JsonIgnore]
        public bool IsOpened => this.Capture.IsOpened();


        private readonly VideoCapture capture;
        private VideoCapture Capture
        {
            get
            {
                // if capture isn't opened try again but only if the previous try was at least 1 minute ago
                if (!this.capture.IsOpened() && DateTime.Now - this.lastUse > TimeSpan.FromMinutes(1))
                {
                    logger.Debug($"Opening camera: {this.Name} ({this.Room.Name})");
                    if (int.TryParse(this.Address, out int device))
                        this.capture.Open(device);
                    else
                        this.capture.Open(this.GetStreamAddress());
                    if (this.capture.IsOpened())
                    {
                        this.capture.Set(CaptureProperty.BufferSize, 1);
                        this.capture.Read(new Mat()); // dump one image to prepare the capture
                    }
                    this.lastUse = DateTime.Now;
                }
                if (this.capture.IsOpened())
                    this.lastUse = DateTime.Now;

                return this.capture;
            }
        }


        private DateTime lastOnline;
        [JsonIgnore]
        [UiProperty]
        public override DateTime LastOnline => this.lastOnline > base.LastOnline ? this.lastOnline : base.LastOnline; // take the soonest image capture or sensor data received

        private DateTime lastUse;
        private DateTime nextDataRead;
        private DateTime lastImageSaved;
        private bool stopped;

        private readonly Dictionary<Type, object> onvif;


        public Camera()
        {
            this.IsOnvifSupported = true;
            this.ReadDataInterval = 15;
            this.Record = true;

            this.capture = new VideoCapture();
            this.lastOnline = DateTime.Now;
            this.lastUse = DateTime.Now.AddMinutes(-1);
            this.nextDataRead = DateTime.Now.AddMinutes(1); // start reading 1 minute after start
            this.lastImageSaved = DateTime.Now.AddMinutes(1);
            this.stopped = false;

            this.onvif = new Dictionary<Type, object>();
        }

        public override void Setup()
        {
            base.Setup();

            // prepare capture for opening
            Task.Run(() => { lock (this.capture) this.Capture.IsOpened(); });

            var thread = new Thread(this.RecordLoop)
            {
                Name = $"{this.Room.Name} {this.Name} Record",
                IsBackground = true
            };
            thread.Start();
        }

        public override void Stop()
        {
            base.Stop();
            this.stopped = true;
        }

        public override void Update()
        {
            base.Update();

            // read sensor data
            if (this.IsOnvifSupported && DateTime.Now > this.nextDataRead)
            {
                var data = this.ReadData();
                if (data != null)
                {
                    this.AddData(DateTime.Now, data);
                    this.nextDataRead = DateTime.Now.AddSeconds(this.ReadDataInterval);
                }
                else // if doesn't succeed wait more before next try
                    this.nextDataRead = DateTime.Now.AddSeconds(this.ReadDataInterval * 4);
            }

            if (this.Record && DateTime.Now - this.lastImageSaved > TimeSpan.FromMinutes(5))
                Alert.Create($"Camera '{this.Name}' ({this.Room.Name}) is inactive!").Validity(TimeSpan.FromHours(1)).Send();
        }

        private void RecordLoop()
        {
            var path = Path.Combine(MyHome.Instance.Config.CameraRecordsPath, $"{this.Room.Name}_{this.Name}");
            Directory.CreateDirectory(path);

            Stopwatch sw = Stopwatch.StartNew();
            while (!this.stopped)
            {
                sw.Restart();
                if (this.Record)
                {
                    try
                    {
                        this.DropOldFrames();
                        this.SaveImage(Path.Combine(path, $"{this.Room.Name}_{this.Name}_{DateTime.Now:HH-mm}.jpg"), false);
                        this.lastImageSaved = DateTime.Now;

                        if (DateTime.Now.Hour == 23 && DateTime.Now.Minute == 59) // if we are in the end of a day
                            Task.Run(() => this.ArchiveRecords()); // use task to prevent inactive alarm trigger
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Failed to record image from camera '{this.Name}' ({this.Room.Name})");
                        logger.Debug(e);
                    }
                }
                var elapsed = sw.Elapsed;
                if (elapsed < TimeSpan.FromMinutes(1))
                    Thread.Sleep(TimeSpan.FromMinutes(1) - elapsed);
            }
        }


        public Mat GetImage(bool initIfEmpty = true, Size? size = null, bool timestamp = true)
        {
            lock (this.capture)
            {
                var image = new Mat();
                this.Capture.Read(image);
                if (image.Empty())
                {
                    // release the camera if it is open and try to open it again next time
                    if (this.Capture.IsOpened())
                        this.Capture.Release();
                    this.lastUse = DateTime.Now.AddMinutes(-1);

                    if (!initIfEmpty)
                        return null;
                    image = new Mat(480, 640, MatType.CV_8UC3, 0);
                    image.Line(0, 0, 640, 480, Scalar.Red, 2, LineTypes.AntiAlias);
                    image.Line(640, 0, 0, 480, Scalar.Red, 2, LineTypes.AntiAlias);
                }
                else
                    this.lastOnline = DateTime.Now;
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
            logger.Trace($"Camera '{this.Name}' ({this.Room.Name}) save image: {filepath}");

            var image = this.GetImage(initIfEmpty, size, timestamp);
            if (image == null)
            {
                logger.Debug($"Camera '{this.Name}' ({this.Room.Name}) failed to save image: {filepath}");
                return false;
            }
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
                logger.Error($"Cannot move camera '{this.Name}' ({this.Room.Name})");
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
                logger.Error($"Cannot restart camera '{this.Name}' ({this.Room.Name})");
                logger.Debug(e);
            }
            return false;
        }


        private string GetStreamAddress()
        {
            // rtsp://192.168.0.120:554/user=admin_password=12345_channel=1_stream=0.sdp?real_stream
            if (this.Address.StartsWith("rtsp://") || !this.IsOnvifSupported)
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
                var ip = this.Address.Split(new char[] { ':', '@' })[2];
                return response.Replace(responseIp, ip);
            }
            catch (Exception e)
            {
                logger.Error($"Cannot get stream address for camera '{this.Name}' ({this.Room.Name})");
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
                    logger.Trace($"Creating Onvif {typeof(T).Name} for camera '{this.Name}' ({this.Room.Name})");
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1168:Empty arrays and collections should be returned instead of null")]
        private Dictionary<string, object> ReadData()
        {
            var result = new Dictionary<string, object>();
            try
            {
                var pullPointClient = this.GetOnvif<PullPointSubscriptionClient>();
                var request = new PullMessagesRequest("PT5S", 100, null);
                var response = pullPointClient.PullMessagesAsync(request).Result;

                var data = response.NotificationMessage[0].Message.ChildNodes[3];
                foreach (XmlNode child in data.ChildNodes)
                {
                    if (child == null || child.Attributes == null)
                        continue;

                    var value = child.Attributes["Value"].Value;
                    result.Add(child.Attributes["Name"].Value, Utils.Utils.ParseValue(value, null));
                }
                return result;
            }
            catch (Exception e)
            {
                logger.Error($"Cannot read data from camera '{this.Name}' ({this.Room.Name})");
                logger.Debug(e);
                return null;
            }
        }

        private void ArchiveRecords()
        {
            // get images older than 24 hours
            var path = Path.Combine(MyHome.Instance.Config.CameraRecordsPath, $"{this.Room.Name}_{this.Name}");
            var images = Directory.GetFiles(path, "*.jpg")
                .Select(f => new FileInfo(f)).Where(f => f.CreationTime >= DateTime.Now.AddHours(-24)).OrderBy(f => f.CreationTime)
                .Select(f => f.FullName).ToList();
            if (images.Count > ArchiveImagesCount)
            {
                // leave only every Nth element if images are too many
                var step = images.Count / ArchiveImagesCount + 1;
                images = images.Where((x, i) => i % step == 0).ToList();
            }

            var videoFilename = $"{this.Room.Name}_{this.Name}_{DateTime.Now.Date:yyyy-MM-dd}.mp4";
            Services.CreateVideo(images, Path.Join(path, videoFilename));

            // cleanup cameras records
            Utils.Utils.CleanupFilesByCapacity(
                Directory.GetFiles(path, "*.mp4")
                    .Select(f => new FileInfo(f)).OrderBy(f => f.CreationTime),
                MyHome.Instance.Config.CameraRecordsDiskUsage, logger);
        }

        private void DropOldFrames()
        {
            lock (this.capture)
            {
                // if capture is not open or we use it less than 1 seconds ago
                if (!this.capture.IsOpened() || DateTime.Now - this.lastUse < TimeSpan.FromSeconds(1))
                    return;

                Stopwatch stopwatch = Stopwatch.StartNew();
                // drop frames until it took less than 100 ms for a frame (not buffered) or timeout - 3 sec
                var start = DateTime.Now;
                var image = new Mat();
                while (stopwatch.Elapsed.Milliseconds < 100 && DateTime.Now - start < TimeSpan.FromSeconds(3))
                {
                    stopwatch.Restart();
                    if (!this.capture.Read(image))
                        break;
                }
            }
        }
    }
}
