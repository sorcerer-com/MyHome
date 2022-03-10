﻿using System;
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
using Newtonsoft.Json.Serialization;

using NLog;

namespace MyHome
{
    public sealed class MyHome : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private int updateInterval = 1; // seconds
        private readonly int upgradeCheckInterval = 5; // minutes
        private readonly int mqttDisconnectedAlert = 1; // minutes
        [JsonProperty]
        private DateTime lastBackupTime;
        private DateTime mqttDisconnectedTime;


        public static MyHome Instance { get; private set; }

        public Config Config { get; }

        [JsonIgnore]
        public GlobalEvent Events { get; }

        [JsonIgnore]
        public MqttClientWrapper MqttClient { get; }

        public List<Room> Rooms { get; }

        public Dictionary<string, BaseSystem> Systems { get; }

        [JsonIgnore]
        public bool SystemChanged { get; set; }

        [JsonIgnore]
        public bool? UpgradeAvailable { get; private set; }


        [JsonIgnore]
        public DevicesSystem DevicesSystem => this.Systems.Values.OfType<DevicesSystem>().FirstOrDefault();

        [JsonIgnore]
        public SecuritySystem SecuritySystem => this.Systems.Values.OfType<SecuritySystem>().FirstOrDefault();

        [JsonIgnore]
        public ActionsSystem ActionsSystem => this.Systems.Values.OfType<ActionsSystem>().FirstOrDefault();


        public MyHome()
        {
            // TODO list 
            // * SecuritySystem - define zones - group of rooms, default zone - all; integrate with actions
            // * UI - mobile / landscape
            //   - show all sensors data on a single chart somewhere
            //   - maybe show devices instead of value / grouped by type -https://miro.medium.com/max/2400/1*MqXRDCodJPM2vIEjygK36A.jpeg
            //   - too many sensor values, merge Water Switch and Water State somehow
            // * multiple sensor graphics at once (for one sensor subname - motion, by multiple devices too)

            logger.Info("Start My Home");
            Instance = this;
            using (var repo = new Repository("."))
            {
                logger.Info($"Version: {repo.Head.Tip.Author.When.ToLocalTime():dd/MM/yyyy HH:mm:ss}" +
                    $" {repo.Head.Tip.Id.Sha[..7]} {repo.Head.Tip.MessageShort}");
            }

            this.Config = new Config();
            this.Events = new GlobalEvent();
            this.MqttClient = new MqttClientWrapper();

            this.Rooms = new List<Room>();

            this.Systems = new Dictionary<string, BaseSystem>();
            foreach (Type type in typeof(BaseSystem).GetSubClasses())
                this.Systems.Add(type.Name, (BaseSystem)Activator.CreateInstance(type));
            this.Systems.TrimExcess();

            this.lastBackupTime = DateTime.Now;
            this.mqttDisconnectedTime = DateTime.Now;
            this.SystemChanged = false;

            this.Load();

            this.Setup();

            var thread = new Thread(this.Update)
            {
                Name = "MyHome update",
                IsBackground = true
            };
            thread.Start();

            this.Events.Fire(this, GlobalEventTypes.Start);
        }

        void IDisposable.Dispose()
        {
            this.Stop();
            GC.SuppressFinalize(this);
        }

        public void Setup()
        {
            logger.Info("Setup My Home");

            var (host, port) = Utils.Utils.SplitAddress(this.Config.MqttServerAddress);
            this.MqttClient.Connect("MyHomeClient", host, port, this.Config.MqttUsername, this.Config.MqttPassword);

            foreach (var system in this.Systems.Values)
                system.Setup();
        }

        public void Stop()
        {
            this.Events.Fire(this, GlobalEventTypes.Stop);
            this.updateInterval = 0;
            this.MqttClient.Disconnect();

            this.Save();
            foreach (var system in this.Systems.Values)
                system.Stop();
            logger.Info("Stop My Home");
        }

        public void Load()
        {
            logger.Info("Load settings and data");
            if (!File.Exists(Config.DataFilePath))
            {
                logger.Warn("Data file doesn't exist");
                this.Save();
                return;
            }

            ITraceWriter traceWriter = new MemoryTraceWriter();
            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                Formatting = Formatting.Indented,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                TraceWriter = traceWriter
            });

            var json = File.ReadAllText(Config.DataFilePath);
            var data = JObject.Parse(json);
            try
            {
                serializer.Populate(data.CreateReader(), this);
            }
            finally
            {
                // logger.Trace(traceWriter);
            }

            this.SystemChanged = false;
            this.Events.Fire(this, GlobalEventTypes.Loaded);
        }

        public void Save()
        {
            logger.Debug("Save settings and data");

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


            // retry since sometimes failed because of "Collection was modified"
            Utils.Utils.Retry(_ =>
            {
                var data = JObject.FromObject(this, serializer);
                var json = data.ToString();
                File.WriteAllText(Config.DataFilePath, json);
            }, 3, logger, "save");

            this.SystemChanged = false;
            this.Events.Fire(this, GlobalEventTypes.Saved);
        }

        private void Update()
        {
            this.CheckForUpgrade();
            Stopwatch stopwatch = new Stopwatch();
            while (this.updateInterval > 0)
            {
                stopwatch.Restart();

                this.Systems.Values.RunForEach(system => system.Update());

                this.CheckMqttStatus();

                var now = DateTime.Now;
                if (now.Minute % this.upgradeCheckInterval == 0 && now.Second < this.updateInterval)
                    this.CheckForUpgrade();

                if (this.SystemChanged)
                    this.Save();

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(this.updateInterval))
                {
                    logger.Debug($"Update time: {stopwatch.Elapsed}");
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromSeconds(this.updateInterval) - stopwatch.Elapsed);
                }
            }
        }

        private void CheckMqttStatus()
        {
            var now = DateTime.Now;
            if (this.MqttClient.IsConnected)
                this.mqttDisconnectedTime = now;
            else if (now - this.mqttDisconnectedTime > TimeSpan.FromMinutes(this.mqttDisconnectedAlert) &&
                now - this.mqttDisconnectedTime < TimeSpan.FromMinutes(this.mqttDisconnectedAlert * 2))
            {
                this.SendAlert($"MQTT broker is down from {this.mqttDisconnectedTime:dd/MM/yyyy HH:mm:ss}!");
                this.mqttDisconnectedTime = now - TimeSpan.FromMinutes(this.mqttDisconnectedAlert * 2);
            }
        }


        public bool SendAlert(string msg, List<string> fileNames = null, bool force = false)
        {
            try
            {
                logger.Info($"Send alert: {msg}");

                bool result = true;
                msg = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\n{msg}";

                var images = new List<string>();
                foreach (var camera in this.DevicesSystem.Cameras)
                {
                    var filename = Path.Combine(Config.BinPath, $"{camera.Room.Name}_{camera.Name}.jpg");
                    camera.SaveImage(filename);
                    images.Add(filename);
                }
                fileNames ??= new List<string>();
                fileNames.AddRange(images);

                var latestData = this.DevicesSystem.Sensors.ToDictionary(s => s.Room.Name + "." + s.Name, s => s.Values);
                var emailMsg = $"{msg}\n{JsonConvert.SerializeObject(latestData, Formatting.Indented)}";
                if (!Services.SendEMail(this.Config.SmtpServerAddress, this.Config.Email, this.Config.EmailPassword,
                    this.Config.Email, "My Home", emailMsg, fileNames))
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
                logger.Error("Cannot send alert message");
                logger.Debug(e);
                return false;
            }
        }

        public bool CheckForUpgrade()
        {
            logger.Debug("Checking for system update");

            try
            {
                using var repo = new Repository(".");
                Commands.Fetch(repo, repo.Head.RemoteName, Array.Empty<string>(), null, "");
                this.UpgradeAvailable = repo.Head.TrackingDetails.BehindBy.GetValueOrDefault() > 0;
            }
            catch (Exception e)
            {
                logger.Error("Cannot check for system update");
                logger.Debug(e);
                this.UpgradeAvailable = false;
            }
            return this.UpgradeAvailable.Value;
        }

        public bool Upgrade()
        {
            logger.Info("Upgrading...");
            try
            {
                using var repo = new Repository(".");
                Commands.Fetch(repo, repo.Head.RemoteName, Array.Empty<string>(), null, "");
                var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
                return Commands.Pull(repo, signature, null).Status != MergeStatus.Conflicts;
            }
            catch (Exception e)
            {
                logger.Error("Cannot update the system");
                logger.Debug(e);
                this.UpgradeAvailable = null;
                return false;
            }
        }
    }
}
