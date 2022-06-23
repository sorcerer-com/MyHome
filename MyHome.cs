using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Jint;

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

        [JsonIgnore]
        private Engine JintEngine { get; }


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
            //   - when playing leave media player open
            // * multiple sensor graphics at once (for one sensor subname - motion, by multiple devices too)
            // * drivers to be sensors too - save state change in time
            // * per person presence reporting - Hristo is home, Dida not

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
            this.JintEngine = new Engine(options =>
            {
                options.LimitMemory(100_000_000); // 100MB
                options.TimeoutInterval(TimeSpan.FromSeconds(5));
                options.MaxStatements(1000);
            });

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

            if (!string.IsNullOrEmpty(this.Config.MqttServerAddress))
            {
                var (host, port) = Utils.Utils.SplitAddress(this.Config.MqttServerAddress);
                this.MqttClient.Connect("MyHomeClient", host, port, this.Config.MqttUsername, this.Config.MqttPassword);
            }

            this.JintEngine.SetValue("DateTime", typeof(DateTime))
                .SetValue("TimeSpan", typeof(TimeSpan))
                .SetValue("Task", typeof(System.Threading.Tasks.Task));
            foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where(type => type.IsEnum))
                this.JintEngine.SetValue(type.Name, type);

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

                this.CheckMqttStatus();

                var now = DateTime.Now;
                if (now.Minute % this.upgradeCheckInterval == 0 && now.Second < this.updateInterval)
                    this.CheckForUpgrade();

                if (this.SystemChanged)
                    this.Save();

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(this.updateInterval))
                {
                    logger.Trace($"Update time: {stopwatch.Elapsed}");
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromSeconds(this.updateInterval) - stopwatch.Elapsed);
                }
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
                Models.Alert.Create("MQTT broker is down").Details($"from {this.mqttDisconnectedTime:dd/MM/yyyy HH:mm:ss}!").Validity(TimeSpan.FromDays(1)).Send();
        }

        private void CheckForUpgrade()
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
        }


        public Alert Alert(string message)
        {
            return Models.Alert.Create($"My Home: {message}");
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

        public bool ExecuteJint(Action<Engine> action, string failMessage = "execute Jint action")
        {
            lock (this.JintEngine)
            {
                try
                {
                    action.Invoke(this.JintEngine);
                }
                catch (Exception e)
                {
                    logger.Error($"Failed to {failMessage}");
                    logger.Debug($"{e}\n {new StackTrace()}");
                    return false;
                }
                return true;
            }
        }
    }
}
