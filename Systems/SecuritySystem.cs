using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using MyHome.Models;
using MyHome.Systems.Devices.Sensors;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

using OpenCvSharp;

namespace MyHome.Systems
{
    public class SecuritySystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private static readonly string ImagesPath = Path.Combine(Config.BinPath, "Images");


        [UiProperty(true, "minutes")]
        public int ActivationDelay { get; set; } // minutes

        [UiProperty(true, "minutes")]
        public int SendInterval { get; set; } // minutes

        [UiProperty(true, "percents")]
        public double MovementThreshold { get; set; } // percents

        [UiProperty(true, "MB")]
        public double ImagesDiskUsage { get; set; } // MB

        [UiProperty(true, "minutes")]
        public int PresenceDetectionInterval { get; set; } // minutes

        [UiProperty(true)]
        public List<string> PresenceDeviceIPs { get; }


        [JsonIgnore]
        public Dictionary<Room, bool> ActivatedRooms
        {
            get
            {
                lock (this.roomsInfo)
                    return this.roomsInfo.ToDictionary(r => r.Room, r => r.Activated);
            }
        }

        public Dictionary<string, Dictionary<DateTime, string>> History { get; } // roomName / time / action

        [JsonIgnore]
        public bool Presence { get; private set; }


        private sealed class RoomInfo
        {
            public Room Room { get; set; }

            public bool Activated { get; set; }

            public DateTime StartTime { get; set; }

            public List<string> ImageFiles { get; set; }
        }

        [JsonProperty]
        private readonly List<RoomInfo> roomsInfo;

        private readonly Dictionary<string, Mat> prevImages;

        private DateTime presenceDetectionTimer;


        public SecuritySystem()
        {
            this.ActivationDelay = 15;
            this.SendInterval = 5;
            this.MovementThreshold = 0.02;
            this.ImagesDiskUsage = 200;
            this.PresenceDetectionInterval = 5;
            this.PresenceDeviceIPs = new List<string>();
            this.History = new Dictionary<string, Dictionary<DateTime, string>>();
            this.Presence = false;

            this.roomsInfo = new List<RoomInfo>();
            this.prevImages = new Dictionary<string, Mat>();
            this.presenceDetectionTimer = DateTime.Now;

            Directory.CreateDirectory(ImagesPath);
        }

        public void SetEnable(Room room, bool enable)
        {
            lock (this.roomsInfo)
            {
                var roomInfo = this.roomsInfo.FirstOrDefault(r => r.Room == room);
                if (enable)
                {
                    if (roomInfo != null)
                    {
                        logger.Debug($"Try to enable security alarm for '{room.Name}' room, but it's already enabled");
                        return;
                    }

                    logger.Info($"Enable security alarm for '{room.Name}' room");
                    this.roomsInfo.Add(new RoomInfo()
                    {
                        Room = room,
                        Activated = false,
                        StartTime = DateTime.Now + TimeSpan.FromMinutes(this.ActivationDelay),
                        ImageFiles = new List<string>()
                    });
                }
                else
                {
                    if (roomInfo == null)
                    {
                        logger.Debug($"Try to disable security alarm for '{room.Name}' room, but it's not enabled");
                        return;
                    }

                    logger.Info($"Disable security alarm for '{room.Name}' room");
                    if (roomInfo.Activated)
                        logger.Warn("The alarm is currently activated");
                    this.roomsInfo.Remove(roomInfo);
                }
                this.SetHistory(room, enable ? "Enabled" : "Disabled");
                MyHome.Instance.SystemChanged = true;
            }
        }

        public void Activate(Room room)
        {
            lock (this.roomsInfo)
            {
                var roomInfo = this.roomsInfo.FirstOrDefault(r => r.Room == room);
                if (roomInfo == null)
                {
                    logger.Trace($"Try to activate security alarm for '{room.Name}' room, but it's not enabled");
                    return;
                }

                if (DateTime.Now - roomInfo.StartTime < TimeSpan.Zero)
                {
                    logger.Debug($"Try to activate security alarm for '{room.Name}' room, but the delay isn't passed");
                    return;
                }

                if (roomInfo.Activated)
                    return;

                roomInfo.Activated = true;
                roomInfo.StartTime = DateTime.Now;
                foreach (var camera in roomInfo.Room.Cameras)
                    this.prevImages.Remove(camera.Room.Name + "." + camera.Name);
                logger.Info($"Security alarm activated in '{room.Name}' room");
                this.SetHistory(room, "Activated");
                MyHome.Instance.SystemChanged = true;
                MyHome.Instance.Events.Fire(this, GlobalEventTypes.SecurityAlarmActivated, roomInfo.Room);
            }
        }


        public override void Update()
        {
            base.Update();

            lock (this.roomsInfo)
            {
                foreach (var roomInfo in this.roomsInfo)
                {
                    if (!roomInfo.Activated)
                        continue;

                    var elapsed = DateTime.Now - roomInfo.StartTime;

                    // send alert
                    if (elapsed > TimeSpan.FromMinutes(this.SendInterval))
                    {
                        roomInfo.Activated = false;
                        this.SetHistory(roomInfo.Room, "Enabled");
                        if (roomInfo.ImageFiles.Count > 1 || CheckCameras(roomInfo.Room))
                        {
                            bool sent = Alert.Create($"'{roomInfo.Room.Name}' room security alarm activated!")
                                .Level(Alert.AlertLevel.Critical)
                                .Filenames(roomInfo.ImageFiles)
                                .Send();
                            if (sent)
                                roomInfo.ImageFiles.Clear();
                        }
                        else
                        {
                            logger.Info($"Skip alert sending for '{roomInfo.Room.Name}' room");
                            roomInfo.ImageFiles.Clear();
                        }
                        this.ClearImages();
                        MyHome.Instance.SystemChanged = true;
                    }

                    foreach (var camera in roomInfo.Room.Cameras)
                    {
                        if (camera.IsOpened) // try to open the camera and if succeed
                            this.SaveImage(camera, roomInfo);
                    }
                }
            }

            if (DateTime.Now - this.presenceDetectionTimer > TimeSpan.FromMinutes(this.PresenceDetectionInterval))
            {
                this.presenceDetectionTimer = DateTime.Now;
                Task.Run(() =>
                {
                    var newPresence = this.DetectPresence();
                    if (newPresence != this.Presence)
                    {
                        this.Presence = newPresence;
                        logger.Info($"{(this.Presence ? "" : "No ")}Presence detected");
                        MyHome.Instance.Events.Fire(this, GlobalEventTypes.PresenceChanged, this.Presence);
                    }
                });
            }
        }


        private void SaveImage(Camera camera, RoomInfo roomInfo)
        {
            var image = camera.GetImage(false);
            if (image == null)
                return;
            if (this.prevImages.ContainsKey(camera.Room.Name + "." + camera.Name))
            {
                // if no movement between current and previous image - skip saving
                if (!FindMovement(this.prevImages[camera.Room.Name + "." + camera.Name], image, this.MovementThreshold))
                    return;
            }
            this.prevImages[camera.Room.Name + "." + camera.Name] = image.Resize(new Size(640, 480));

            var filename = $"{camera.Room.Name}_{camera.Name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg";
            var filepath = Path.Combine(ImagesPath, filename);
            if (Cv2.ImWrite(filepath, image))
                roomInfo.ImageFiles.Add(filepath);
        }

        private void ClearImages()
        {
            var files = Directory.GetFiles(ImagesPath, "*.jpg").Select(f => new FileInfo(f));
            var total = files.Sum(fi => fi.Length);
            if (total < this.ImagesDiskUsage * 1024L * 1024L) // ImageDiskUsage is in MB
                return;

            var deleted = 0L;
            foreach (var fileInfo in files.OrderBy(f => f.CreationTime))
            {
                deleted += fileInfo.Length;
                File.Delete(fileInfo.FullName);

                if (total - deleted < this.ImagesDiskUsage * 1024L * 1024L * 0.9) // reach 90%
                    break;
            }
        }

        private bool DetectPresence()
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            // retry 5 times
            for (int i = 0; i < 5; i++)
            {
                foreach (var ip in this.PresenceDeviceIPs)
                {
                    try
                    {
                        var reply = ping.Send(System.Net.IPAddress.Parse(ip));
                        if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            return true;
                    }
                    catch (Exception e)
                    {
                        if (i == 0)
                        {
                            logger.Error($"Failed to detect presence for {ip}");
                            logger.Debug(e);
                        }
                    }
                }
            }
            return false;
        }

        private void SetHistory(Room room, string action)
        {
            if (!this.History.ContainsKey(room.Name))
                this.History.Add(room.Name, new Dictionary<DateTime, string>());

            this.History[room.Name][DateTime.Now] = action;

            // remove entries older than one week
            var weekAgo = DateTime.Now.AddDays(-7);
            foreach (var key in this.History.Keys)
            {
                foreach (var time in this.History[key].Keys)
                {
                    if (time < weekAgo)
                        this.History[key].Remove(time);
                }
            }
        }


        private static bool CheckCameras(Room room)
        {
            // it there is no cameras or there is offline camera
            return !room.Cameras.Any() || room.Cameras.Any(camera => !camera.IsOpened);
        }

        private static bool FindMovement(Mat image1, Mat image2, double threshold)
        {
            var gray1 = image1
                .Resize(new Size(640, 480))
                .CvtColor(ColorConversionCodes.BGR2GRAY)
                .GaussianBlur(new Size(21, 21), 0);
            var gray2 = image2
                .Resize(new Size(640, 480))
                .CvtColor(ColorConversionCodes.BGR2GRAY)
                .GaussianBlur(new Size(21, 21), 0);

            Mat diff = new Mat();
            Cv2.Absdiff(gray1, gray2, diff);
            diff = diff.Threshold(25, 255, ThresholdTypes.Binary);
            var diffPecent = (double)diff.CountNonZero() / (640 * 480);
            return diffPecent > threshold;
        }
    }
}
