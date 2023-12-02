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

        [UiProperty(true, "minutes")]
        public int PresenceDetectionInterval { get; set; } // minutes

        [UiProperty(true, "person/device IP")]
        public Dictionary<string, string> PresenceDeviceIPs { get; }


        [UiProperty]
        public Dictionary<string, Dictionary<DateTime, string>> History { get; } // roomName / time / action

        [JsonIgnore]
        [UiProperty]
        public List<string> Present { get; private set; } // list of present people


        [JsonIgnore]
        public Dictionary<Room, bool> ActivatedRooms
        {
            get
            {
                lock (this.roomsInfo)
                    return this.roomsInfo.ToDictionary(r => r.Room, r => r.Activated);
            }
        }


        private sealed class RoomInfo
        {
            public Room Room { get; set; }

            public int EnableLevel { get; set; }

            public bool Activated { get; set; }

            public DateTime StartTime { get; set; }

            public List<string> ImageFiles { get; set; }
        }

        [JsonProperty]
        private readonly List<RoomInfo> roomsInfo;

        private readonly Dictionary<string, byte[]> prevImages;

        private DateTime presenceDetectionTimer;


        public SecuritySystem()
        {
            this.ActivationDelay = 15;
            this.SendInterval = 5;
            this.MovementThreshold = 0.02;
            this.PresenceDetectionInterval = 1;
            this.PresenceDeviceIPs = new Dictionary<string, string>();

            this.History = new Dictionary<string, Dictionary<DateTime, string>>();
            this.Present = new List<string>();

            this.roomsInfo = new List<RoomInfo>();
            this.prevImages = new Dictionary<string, byte[]>();
            this.presenceDetectionTimer = DateTime.Now - TimeSpan.FromMinutes(this.PresenceDetectionInterval);

            Directory.CreateDirectory(MyHome.Instance.Config.CameraRecordsPath);
        }

        public void SetEnable(Room room, bool enable, int level = 0)
        {
            lock (this.roomsInfo)
            {
                var roomInfo = this.roomsInfo.Find(r => r.Room == room);
                if (enable)
                {
                    if (roomInfo != null)
                    {
                        logger.Debug($"Try to enable({level}) security alarm for '{room.Name}' room, but it's already enabled({roomInfo.EnableLevel})");
                        if (roomInfo.EnableLevel > level)
                            roomInfo.EnableLevel = level;
                        return;
                    }

                    logger.Info($"Enable({level}) security alarm for '{room.Name}' room");
                    this.roomsInfo.Add(new RoomInfo()
                    {
                        Room = room,
                        EnableLevel = level,
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
                    if (roomInfo.EnableLevel < level)
                    {
                        logger.Debug($"Try to disable({level}) security alarm for '{room.Name}' room, but it is enabled with higher level({roomInfo.EnableLevel})");
                        return;
                    }

                    logger.Info($"Disable({level}) security alarm for '{room.Name}' room");
                    if (roomInfo.Activated)
                        logger.Warn("The alarm is currently activated");
                    else
                        roomInfo.ImageFiles.ForEach(File.Delete);
                    this.roomsInfo.Remove(roomInfo);
                }
                this.SetHistory(room.Name, enable ? "Enabled" : "Disabled");
                MyHome.Instance.SystemChanged = true;
            }
        }

        public void Activate(Room room)
        {
            lock (this.roomsInfo)
            {
                var roomInfo = this.roomsInfo.Find(r => r.Room == room);
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

                // play security alarm on speakers if no presence detected
                Task.Delay(TimeSpan.FromMinutes(this.PresenceDetectionInterval * 3)).ContinueWith(t =>
                {
                    if (this.roomsInfo.Find(r => r.Room == room)?.Activated == true)
                        MyHome.Instance.PlayAlarm(Devices.Drivers.Types.ISpeakerDriver.AlarmType.Security);
                });

                logger.Info($"Security alarm activated in '{room.Name}' room");
                this.SetHistory(room.Name, "Activated");
                MyHome.Instance.SystemChanged = true;
                MyHome.Instance.Events.Fire(this, GlobalEventTypes.SecurityAlarmActivated, roomInfo.Room);
            }
        }


        protected override void Update()
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
                        this.SetHistory(roomInfo.Room.Name, "Enabled");
                        if (roomInfo.ImageFiles.Count > 1 || CheckCameras(roomInfo.Room))
                        {
                            bool sent = Alert.Create($"'{roomInfo.Room.Name}' room security alarm activated!")
                                .Level(Alert.AlertLevel.Critical)
                                .Filenames(roomInfo.ImageFiles)
                                .Send();
                            if (sent)
                            {
                                roomInfo.ImageFiles.ForEach(File.Delete);
                                roomInfo.ImageFiles.Clear();
                            }
                        }
                        else
                        {
                            logger.Info($"Skip alert sending for '{roomInfo.Room.Name}' room");
                            roomInfo.ImageFiles.ForEach(File.Delete);
                            roomInfo.ImageFiles.Clear();
                        }

                        MyHome.Instance.SystemChanged = true;
                    }

                    // use task since Camera getImage can block
                    Task.Run(() =>
                    {
                        foreach (var camera in roomInfo.Room.Cameras)
                        {
                            if (camera.IsOpened()) // try to open the camera and if succeed
                                this.SaveImage(camera, roomInfo);
                        }
                    });
                }
            }

            // presence detection
            if (DateTime.Now - this.presenceDetectionTimer > TimeSpan.FromMinutes(this.PresenceDetectionInterval))
            {
                this.presenceDetectionTimer = DateTime.Now;
                Task.Run(() =>
                {
                    var newPresence = this.DetectPresence().OrderBy(x => x);

                    if (!newPresence.SequenceEqual(this.Present))
                    {
                        foreach (var name in this.PresenceDeviceIPs.Keys)
                        {
                            if (newPresence.Contains(name) && !this.Present.Contains(name)) // new present
                                this.SetHistory(name, "Present");
                            else if (!newPresence.Contains(name) && this.Present.Contains(name)) // someone left
                                this.SetHistory(name, "Left");
                        }

                        if (newPresence.Any() != this.Present.Any()) // if there is a change in presence at all, not per person 
                            MyHome.Instance.Events.Fire(this, GlobalEventTypes.PresenceChanged, newPresence.Any());

                        logger.Info(newPresence.Any() ? "Presence change detected: " + string.Join(", ", newPresence) : "No presence");
                        this.Present = newPresence.ToList();
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
                var diffPercent = camera.DiffImages(this.prevImages[camera.Room.Name + "." + camera.Name], image);
                if (diffPercent < this.MovementThreshold)
                    return;
            }
            this.prevImages[camera.Room.Name + "." + camera.Name] = image;

            var filename = $"{camera.Room.Name}_{camera.Name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg";
            var filepath = Path.Combine(MyHome.Instance.Config.CameraRecordsPath, filename);

            try
            {
                File.WriteAllBytes(filepath, image);
                roomInfo.ImageFiles.Add(filepath);
            }
            catch { /* nothing to do */ }
        }

        private List<string> DetectPresence()
        {
            var result = new List<string>();
            using var ping = new System.Net.NetworkInformation.Ping();
            // retry 5 times
            for (int i = 0; i < 5; i++)
            {
                foreach (var kvp in this.PresenceDeviceIPs)
                {
                    if (result.Contains(kvp.Key))
                        continue;
                    try
                    {
                        var reply = ping.Send(System.Net.IPAddress.Parse(kvp.Value), 1000);
                        logger.Trace($"Ping: {kvp.Value}, {reply.RoundtripTime}, {reply.Status}");
                        if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                            result.Add(kvp.Key);
                        if (result.Count == this.PresenceDeviceIPs.Count) // all devices are present
                            return result;
                    }
                    catch (Exception e)
                    {
                        if (i == 0)
                        {
                            logger.Error($"Failed to detect presence for {kvp.Key} ({kvp.Value})");
                            logger.Debug(e);
                        }
                    }
                }
            }
            return result;
        }

        private void SetHistory(string actor, string action)
        {
            if (!this.History.ContainsKey(actor))
                this.History.Add(actor, new Dictionary<DateTime, string>());

            this.History[actor][DateTime.Now] = action;

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
            return !room.Cameras.Any() || room.Cameras.Any(camera => !camera.IsOpened());
        }
    }
}
