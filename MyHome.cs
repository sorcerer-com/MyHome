﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

using Jint;

using LibGit2Sharp;

using MyHome.Models;
using MyHome.Systems;
using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using NLog;
using NLog.LayoutRenderers;

namespace MyHome
{
    public sealed class MyHome : IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private int updateInterval = 1; // seconds
        private readonly int upgradeCheckInterval = 5; // minutes
        private readonly int mqttDisconnectedAlert = 1; // minutes
        private readonly int mainServerDisconnectedAlert = 1; // minutes
        [JsonProperty]
        private DateTime lastBackupTime;
        private DateTime mqttDisconnectedTime;
        private DateTime mainServerDisconnectedTime;


        public static MyHome Instance { get; private set; }


        public Config Config { get; }

        public SongsManager SongsManager { get; }

        [JsonIgnore]
        public GlobalEvent Events { get; }

        [JsonIgnore]
        public MqttClientWrapper MqttClient { get; }

        [JsonIgnore]
        private Engine JintEngine { get; }

        [JsonIgnore]
        public bool BackupMode { get; private set; }


        public List<Room> Rooms { get; }

        public Dictionary<string, BaseSystem> Systems { get; }

        [JsonIgnore]
        public bool SystemChanged { get; set; }

        [JsonIgnore]
        public List<Notification> Notifications { get; }


        [JsonIgnore]
        public DevicesSystem DevicesSystem => this.Systems.Values.OfType<DevicesSystem>().FirstOrDefault();

        [JsonIgnore]
        public SecuritySystem SecuritySystem => this.Systems.Values.OfType<SecuritySystem>().FirstOrDefault();

        [JsonIgnore]
        public ActionsSystem ActionsSystem => this.Systems.Values.OfType<ActionsSystem>().FirstOrDefault();


        public MyHome()
        {
            // TODO list
            // * Update .Net version(LTS), NuGet packages and UI libraries (charts, vue, etc.)
            // * SecuritySystem - define zones - group of rooms, default zone - all; integrate with actions
            // * UI - mobile / landscape (https://miro.medium.com/max/2400/1*MqXRDCodJPM2vIEjygK36A.jpeg)
            //   - new sensor UI - add limits (like unhealthy, alerts, etc.)
            //   - security system modal with sensor statuses (muk, motion, etc), etc.
            //   - improve Speaker UI - multiple playlists
            //   - improve power consumption UI (as plugin somehow)
            //   - revise inline styles
            // * drivers to be sensors too - save state change in time
            // * External system (rpi2, agent) ping system and notify on problem?
            // * Improve devices auto discovery - speaker, ip camera
            // * Improve camera movement capability - move to specific point, saved positions

            logger.Info("Start My Home");
            Instance = this;
            using (var repo = new Repository("."))
            {
                logger.Info($"Version: {repo.Head.Tip.Author.When.ToLocalTime():dd/MM/yyyy HH:mm:ss}" +
                    $" {repo.Head.Tip.Id.Sha[..7]} {repo.Head.Tip.MessageShort}");
            }

            this.Config = new Config();
            this.SongsManager = new SongsManager();
            this.Events = new GlobalEvent();
            this.MqttClient = new MqttClientWrapper();
            this.JintEngine = new Engine(options =>
            {
                options.LimitMemory(100_000_000); // 100MB
                options.TimeoutInterval(TimeSpan.FromMinutes(15)); // to allow "async" ops like Task.Delay(...)
                options.MaxStatements(1000);
            });

            this.Rooms = new List<Room>();

            this.Systems = new Dictionary<string, BaseSystem>();
            foreach (Type type in typeof(BaseSystem).GetSubClasses())
                this.Systems.Add(type.Name, (BaseSystem)Activator.CreateInstance(type));
            this.Systems.TrimExcess();

            this.lastBackupTime = DateTime.Now;
            this.mqttDisconnectedTime = DateTime.Now;
            this.mainServerDisconnectedTime = DateTime.Now;
            this.SystemChanged = false;
            this.Notifications = new List<Notification>();

            this.Load();

            this.Setup();

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

            if (!string.IsNullOrEmpty(this.Config.MainServer))
            {
                this.BackupMode = true;
                this.AddNotification(Notification.BackupModeType, "Backup Mode", ifNotExist: false);
            }

            if (!string.IsNullOrEmpty(this.Config.MqttServerAddress))
            {
                var (host, port) = Utils.Utils.SplitAddress(this.Config.MqttServerAddress);
                var password = Encoding.UTF8.GetString(Convert.FromBase64String(this.Config.MqttPassword));
                this.MqttClient.Connect("MyHomeClient", host, port, this.Config.MqttUsername, password);
            }

            this.JintEngine.SetValue("DateTime", typeof(DateTime))
                .SetValue("TimeSpan", typeof(TimeSpan))
                .SetValue("Task", typeof(System.Threading.Tasks.Task));
            foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsEnum))
                this.JintEngine.SetValue(type.Name, type);
            this.JintEngine.Evaluate("var globals = {};"); // add global dictionary

            foreach (var system in this.Systems.Values)
                system.Setup();

            var thread = new Thread(this.Update)
            {
                Name = "MyHome Update",
                IsBackground = true
            };
            thread.Start();
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
            catch (Exception e)
            {
                logger.Error("Cannot load system");
                logger.Debug(e);
                Models.Alert.Create("Cannot load system")
                    .Validity(TimeSpan.FromHours(1))
                    .Send();
                this.AddNotification(Notification.ProblemType, "Cannot load system", ifNotExist: false);
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
                // make 2 backups
                if (File.Exists(Config.DataFilePath + ".bak"))
                    File.Copy(Config.DataFilePath + ".bak", Config.DataFilePath + ".bak2", true);
                if (File.Exists(Config.DataFilePath))
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
                var json = data.ToString(this.Config.SavePrettyJson ? Formatting.Indented : Formatting.None);
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

                this.CheckMqttStatus();
                this.CheckMainServerStatus();
                this.AutoUpgradeBackupServer();

                var now = DateTime.Now;
                if (now.Minute % this.upgradeCheckInterval == 0 && now.Second < this.updateInterval)
                    this.CheckForUpgrade();

                if (this.SystemChanged)
                    this.Save();

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(this.updateInterval))
                    logger.Trace($"Update time: {stopwatch.Elapsed}");
                else
                    Thread.Sleep(TimeSpan.FromSeconds(this.updateInterval) - stopwatch.Elapsed);
            }
        }

        private void CheckMqttStatus()
        {
            if (string.IsNullOrEmpty(this.Config.MqttServerAddress))
                return;

            var now = DateTime.Now;
            if (this.MqttClient.IsConnected)
                this.mqttDisconnectedTime = now;
            else if (now - this.mqttDisconnectedTime > TimeSpan.FromMinutes(this.mqttDisconnectedAlert))
            {
                Models.Alert.Create("MQTT broker is down")
                    .Details($"from {this.mqttDisconnectedTime:dd/MM/yyyy HH:mm:ss}!")
                    .Validity(TimeSpan.FromHours(1))
                    .Send();
            }

            if (now - this.MqttClient.LastMessageReceived > TimeSpan.FromMinutes(this.DevicesSystem.SensorsCheckInterval))
            {
                Models.Alert.Create("No MQTT messages")
                    .Details($"from {this.MqttClient.LastMessageReceived:dd/MM/yyyy HH:mm:ss}!")
                    .Validity(TimeSpan.FromHours(1))
                    .Send();
            }
        }

        private void CheckMainServerStatus()
        {
            if (string.IsNullOrEmpty(this.Config.MainServer))
                return;

            var result = true;
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                result = client.GetAsync($"{this.Config.MainServer}/api/status").Result.IsSuccessStatusCode;
            }
            catch
            {
                result = false;
            }

            var now = DateTime.Now;
            if (result)
            {
                if (!this.BackupMode)
                {
                    logger.Warn("Main server instance is back online. Return to Backup mode.");
                    this.AddNotification(Notification.BackupModeType, "Backup Mode", ifNotExist: false);
                }
                this.mainServerDisconnectedTime = now;
                this.BackupMode = true;
            }
            else
            {
                // exit backup mode if no response from main server for 10 seconds
                if (now - this.mainServerDisconnectedTime > TimeSpan.FromSeconds(10) &&
                    this.BackupMode)
                {
                    logger.Warn("Main server instance is down. Exit Backup mode.");
                    this.BackupMode = false;
                    this.RemoveNotification(Notification.BackupModeType);
                }

                if (now - this.mainServerDisconnectedTime > TimeSpan.FromMinutes(this.mainServerDisconnectedAlert))
                {
                    Models.Alert.Create("Main MyHome server is down")
                        .Details($"from {this.mainServerDisconnectedTime:dd/MM/yyyy HH:mm:ss}!")
                        .Validity(TimeSpan.FromHours(1))
                        .Send(true);
                }
            }

        }

        private void AutoUpgradeBackupServer()
        {
            // try to upgrade the system if it is backup instance
            if (!string.IsNullOrEmpty(this.Config.MainServer) &&
                this.Notifications.Exists(n => n.Type == Notification.UpgradeType && !n.Message.Contains("failed")))
            {
                if (this.Upgrade())
                {
                    this.Stop();
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => Environment.Exit(0));
                }
                else
                {
                    Models.Alert.Create("Cannot upgrade backup server")
                        .Validity(TimeSpan.FromDays(1))
                        .Send(true);
                }
            }
        }

        private void CheckForUpgrade()
        {
            logger.Debug("Checking for system update");

            try
            {
                using var repo = new Repository(".");
                Commands.Fetch(repo, repo.Head.RemoteName, Array.Empty<string>(), null, "");
                if (repo.Head.TrackingDetails.BehindBy.GetValueOrDefault() > 0)
                    this.AddNotification(Notification.UpgradeType, "System upgrade available");
            }
            catch (Exception e)
            {
                logger.Error("Cannot check for system update");
                logger.Debug(e);
            }
        }


        public Alert Alert(string message)
        {
            return Models.Alert.Create($"My Home: {message}");
        }

        public void AddNotification(string type, string message, TimeSpan? duration = null, bool ifNotExist = true)
        {
            this.Notifications.RemoveAll(n => n.Expired);

            if (ifNotExist && this.Notifications.Exists(n => n.Type == type))
                return;

            if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(message))
            {
                var notification = new Notification(type, message, duration);
                this.Notifications.Add(notification);
            }
        }

        public void RemoveNotification(string type)
        {
            this.Notifications.RemoveAll(n => n.Type == type);
        }

        public void PlayAlarm(ISpeakerDriver.AlarmType alarmType)
        {
            foreach (var speaker in this.DevicesSystem.Devices.OfType<ISpeakerDriver>())
                speaker.PlayAlarm(alarmType);
        }

        public bool Upgrade()
        {
            logger.Info("Upgrading...");
            try
            {
                using var repo = new Repository(".");
                Commands.Fetch(repo, repo.Head.RemoteName, Array.Empty<string>(), null, "");
                repo.Reset(ResetMode.Hard, repo.Head.TrackingDetails.CommonAncestor); // reset local changes if any
                var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
                return Commands.Pull(repo, signature, null).Status != MergeStatus.Conflicts;
            }
            catch (Exception e)
            {
                logger.Error("Cannot update the system");
                logger.Debug(e);
                this.RemoveNotification(Notification.UpgradeType);
                this.AddNotification(Notification.UpgradeType, "System upgrade failed");
                return false;
            }
        }

        public bool ExecuteJint(Action<Engine> action, string failMessage = "execute Jint action")
        {
            lock (this.JintEngine)
            {
                try
                {
                    this.JintEngine
                        .SetValue("logger", logger)
                        .SetValue("myHome", this)
                        .SetValue("Rooms", this.Rooms.ToDictionary(r => r.Name.Replace(" ", "")))
                        .SetValue("Devices", this.Rooms.ToDictionary(r => r.Name.Replace(" ", ""),
                                                r => r.Devices.ToDictionary(d => d.Name.Replace(" ", ""))));

                    action.Invoke(this.JintEngine);
                }
                catch (Exception e)
                {
                    logger.Error($"Failed to {failMessage}: {e.Message}");
                    logger.Debug($"{e}\n {new StackTrace()}");
                    return false;
                }
                return true;
            }
        }
    }
}
