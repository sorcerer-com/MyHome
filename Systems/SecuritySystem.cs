using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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


        [UiProperty(true, "minutes")]
        public int ActivationDelay { get; set; } // minutes

        [UiProperty(true, "minutes")]
        public int SendInterval { get; set; } // minutes

        [UiProperty(true, "percents")]
        public double MovementThreshold { get; set; } // percents

        [JsonIgnore]
        public Dictionary<Room, bool> ActivatedRooms => this.RoomsInfo.ToDictionary(r => r.Room, r => r.Activated);

        private class RoomInfo
        {
            public Room Room { get; set; }

            public bool Activated { get; set; }

            public DateTime StartTime { get; set; }

            public List<string> ImageFiles { get; set; }
        }

        [JsonRequired]
        private List<RoomInfo> RoomsInfo { get; }

        private Dictionary<string, Mat> PrevImages { get; }


        private SecuritySystem() : this(null) { }  // for json deserialization

        public SecuritySystem(MyHome owner) : base(owner)
        {
            this.ActivationDelay = 15;
            this.SendInterval = 5;
            this.MovementThreshold = 0.02;

            this.RoomsInfo = new List<RoomInfo>();
            this.PrevImages = new Dictionary<string, Mat>();
        }

        public void SetEnable(Room room, bool enable)
        {
            var roomInfo = this.RoomsInfo.FirstOrDefault(r => r.Room == room);
            if (enable)
            {
                if (roomInfo != null)
                {
                    logger.Warn($"Try to enable security alarm for '{room.Name}' room, but it's already enabled");
                    return;
                }

                logger.Info($"Enable security alarm for '{room.Name}' room");
                this.RoomsInfo.Add(new RoomInfo()
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
                ClearImages(roomInfo);
                this.RoomsInfo.Remove(roomInfo);
            }
            this.Owner.SystemChanged = true;
        }

        public void Activate(Room room)
        {
            var roomInfo = this.RoomsInfo.FirstOrDefault(r => r.Room == room);
            if (roomInfo == null)
            {
                logger.Debug($"Try to activate security alarm for '{room.Name}' room, but it's not enabled");
                return;
            }

            if (DateTime.Now - roomInfo.StartTime < TimeSpan.Zero)
            {
                logger.Info($"Try to activate security alarm for '{room.Name}' room, but the delay isn't passed");
                return;
            }

            if (roomInfo.Activated)
                return;

            roomInfo.Activated = true;
            roomInfo.StartTime = DateTime.Now;
            foreach (var camera in roomInfo.Room.Cameras)
                this.PrevImages.Remove(camera.Room.Name + "." + camera.Name);
            logger.Info($"Alarm security activated in '{room.Name}' room");
            this.Owner.Events.Fire(this, "SecurityAlarmActivated", roomInfo.Room);
            this.Owner.SystemChanged = true;
        }


        public override void Update()
        {
            base.Update();

            foreach (var roomInfo in this.RoomsInfo)
            {
                if (!roomInfo.Activated)
                    continue;

                var elapsed = DateTime.Now - roomInfo.StartTime;
                bool changed = false;

                // send alert
                if (elapsed > TimeSpan.FromMinutes(this.SendInterval))
                {
                    roomInfo.Activated = false;
                    if (roomInfo.ImageFiles.Count > 1 || CheckCameras(roomInfo.Room))
                    {
                        if (this.Owner.SendAlert($"'{roomInfo.Room.Name}' room security alarm activated!", roomInfo.ImageFiles, true))
                            ClearImages(roomInfo);
                    }
                    else
                    {
                        logger.Info($"Skip alert sending for '{roomInfo.Room.Name}' room");
                        ClearImages(roomInfo);
                    }
                    changed = true;
                }

                foreach (var camera in roomInfo.Room.Cameras)
                {
                    if (camera.IsOpened) // try to open the camera and if succeed
                        changed |= this.SaveImage(camera, roomInfo);
                }
                this.Owner.SystemChanged = changed;
            }
        }

        private bool SaveImage(Camera camera, RoomInfo roomInfo)
        {
            var image = camera.GetImage(false);
            if (image == null)
                return false;
            if (this.PrevImages.ContainsKey(camera.Room.Name + "." + camera.Name))
            {
                // if no movement between current and previous image - skip saving
                if (!FindMovement(this.PrevImages[camera.Room.Name + "." + camera.Name], image, this.MovementThreshold))
                    return false;
            }
            this.PrevImages[camera.Room.Name + "." + camera.Name] = image.Resize(new Size(640, 480));

            var filename = $"{camera.Room.Name}_{camera.Name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg";
            var filepath = Path.Combine(Config.BinPath, filename);
            if (Cv2.ImWrite(filepath, image))
            {
                roomInfo.ImageFiles.Add(filepath);
                return true;
            }
            return false;
        }

        private static void ClearImages(RoomInfo roomInfo)
        {
            foreach (var file in roomInfo.ImageFiles)
                File.Delete(file);

            roomInfo.ImageFiles.Clear();
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
