using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using LibGit2Sharp;

using MyHome.Models;
using MyHome.Systems;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome
{
    // TODO: docstrings?
    public class MyHome : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly Thread thread;
        private int updateInterval = 3; // seconds
        private readonly int upgradeCheckInterval = 5; // minutes
        [JsonRequired]
        private DateTime lastBackupTime;

        public Config Config { get; private set; }

        [JsonIgnore]
        public GlobalEvent Events { get; }

        public List<Room> Rooms { get; private set; }

        public Dictionary<string, BaseSystem> Systems { get; }

        [JsonIgnore]
        public bool SystemChanged { get; set; }

        [JsonIgnore]
        public bool UpgradeAvailable { get; private set; }


        [JsonIgnore]
        public DevicesSystem DevicesSystem => this.Systems.Values.OfType<DevicesSystem>().FirstOrDefault();

        [JsonIgnore]
        public SecuritySystem SecuritySystem => this.Systems.Values.OfType<SecuritySystem>().FirstOrDefault();


        public MyHome()
        {
            logger.Info("Start My Home");


            using (var repo = new Repository("."))
            {
                logger.Info($"Version: {repo.Head.Tip.Author.When.ToLocalTime():dd/MM/yyyy HH:mm:ss} {repo.Head.Tip.MessageShort}");
            }
            // TODO: UI

            this.Config = new Config();
            this.Events = new GlobalEvent();

            this.Rooms = new List<Room>();

            this.Systems = new Dictionary<string, BaseSystem>();
            foreach (Type type in typeof(BaseSystem).GetSubClasses())
                this.Systems.Add(type.Name, (BaseSystem)Activator.CreateInstance(type, this));
            // TODO: Trigger-Action system (migrate Schedule system to it - time trigger; sensor data alerts and light on skill also)
            // TODO: SecuritySystem - enable for all rooms, define zones - group of rooms

            this.lastBackupTime = DateTime.Now;
            this.SystemChanged = false;

            this.Load();

            this.Setup();

            this.thread = new Thread(this.Update)
            {
                Name = "MyHome update",
                IsBackground = true
            };
            this.thread.Start();

            this.Events.Fire(this, "Start");
        }

        void IDisposable.Dispose()
        {
            this.Stop();
            GC.SuppressFinalize(this);
        }

        public void Setup()
        {
            logger.Info("Setup My Home");
            foreach (var system in this.Systems.Values)
                system.Setup();
        }

        public void Stop()
        {
            this.Events.Fire(this, "Stop");
            this.updateInterval = 0;

            this.Save();
            foreach (var system in this.Systems.Values)
                system.Stop();
            logger.Info("Stop My Home");
        }

        public void Load()
        {
            // TODO: transfer the old data to new format
            logger.Info("Load settings and data");
            if (!File.Exists(Config.DataFilePath))
            {
                logger.Warn("Data file doesn't exist");
                return;
            }

            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                Formatting = Formatting.Indented,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            });

            var json = File.ReadAllText(Config.DataFilePath);
            var data = JObject.Parse(json);
            serializer.Populate(data.CreateReader(), this);

            this.SystemChanged = false;
            this.Events.Fire(this, "Loaded");
        }

        public void Save()
        {
            logger.Info("Save settings and data");

            // backup data file every day
            if (DateTime.Now - this.lastBackupTime > TimeSpan.FromDays(1))
            {
                this.lastBackupTime = DateTime.Now;
                File.Copy(Config.DataFilePath, Config.DataFilePath + ".bak", true);
            }

            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                Formatting = Formatting.Indented,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            });

            var data = JObject.FromObject(this, serializer);
            var json = data.ToString();
            File.WriteAllText(Config.DataFilePath, json);

            this.SystemChanged = false;
            this.Events.Fire(this, "Saved");
        }

        private void Update()
        {
            Stopwatch stopwatch = new Stopwatch();
            while (this.updateInterval > 0)
            {
                stopwatch.Restart();

                this.Systems.Values.RunForEach(system => system.Update());

                var now = DateTime.Now;
                if (now.Minute % this.upgradeCheckInterval == 0 && now.Second < this.updateInterval)
                    this.Upgrade(true);

                if (this.SystemChanged)
                    this.Save();

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(this.updateInterval))
                {
                    logger.Warn($"Update time: {stopwatch.Elapsed}");
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromSeconds(this.updateInterval) - stopwatch.Elapsed);
                }
            }
        }

        public bool SendAlert(string msg, List<string> fileNames = null, bool force = false)
        {
            try
            {
                logger.Info($"Send alert {msg}");

                bool result = true;
                var latestData = this.DevicesSystem.Sensors.ToDictionary(s => s.Name, s => s.LastValues);
                msg = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\n{msg}\n{JsonConvert.SerializeObject(latestData)}";

                var images = new List<string>();
                if (fileNames == null)
                {
                    foreach (var camera in this.DevicesSystem.Cameras)
                    {
                        var filename = Path.Combine(Config.BinPath, $"{camera.Room.Name}_{camera.Name}.jpg");
                        camera.SaveImage(filename);
                        images.Add(filename);
                    }
                    fileNames = images;
                }

                if (!Services.SendEMail(this.Config.SmtpServerAddress, this.Config.Email, this.Config.EmailPassword,
                    this.Config.Email, "My Home", msg, fileNames))
                {
                    result = false;
                }

                foreach (var file in images)
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }

                var quietHours = this.Config.QuietHours.Split("-", StringSplitOptions.TrimEntries).Select(h => int.Parse(h)).ToArray();
                if (force)
                    quietHours = new int[] { 0, 0 };

                var currHour = DateTime.Now.Hour;
                if (quietHours[0] > quietHours[1] && (currHour > quietHours[0] || currHour < quietHours[1]))
                {
                    logger.Info($"Quiet hours: {quietHours[0]} - {quietHours[1]}");
                }
                else if (quietHours[0] < quietHours[1] && (currHour > quietHours[0] && currHour < quietHours[1]))
                {
                    logger.Info($"Quiet hours: {quietHours[0]} - {quietHours[1]}");
                }
                else
                {
                    if (!Services.SendSMS(this.Config.GsmNumber, "telenor", this.Config.MyTelenorPassword, msg))
                    {
                        result = false;
                        Services.SendEMail(this.Config.SmtpServerAddress, this.Config.Email, this.Config.EmailPassword,
                            this.Config.Email, "My Home", "Alert sending failed");
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                logger.Error(e, "Cannot send alert message");
                return false;
            }
        }

        private bool Upgrade(bool checkOnly)
        {
            if (checkOnly)
                logger.Debug("Checking for system update");

            try
            {
                using var repo = new Repository(".");
                Commands.Fetch(repo, repo.Head.RemoteName, Array.Empty<string>(), null, "");
                if (checkOnly)
                {
                    this.UpgradeAvailable = repo.Head.TrackingDetails.BehindBy.GetValueOrDefault() > 0;
                    return this.UpgradeAvailable;
                }
                else
                {
                    logger.Info("Upgrading...");
                    var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
                    Commands.Pull(repo, signature, null);
                }
                return true;
            }
            catch (Exception e)
            {
                if (checkOnly)
                    logger.Error(e, "Cannot check for system update");
                else
                    logger.Error(e, "Cannot update the system");
                return false;
            }
        }
    }
}
