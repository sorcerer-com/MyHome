using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

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
        private DateTime lastBackupTime;

        public Config Config { get; private set; }

        public Dictionary<string, BaseSystem> Systems { get; }

        public bool SystemChanged { get; set; }

        public MyHome()
        {
            logger.Info("Start My Home");
            // TODO: log current version (commit info)

            this.Config = new Config();

            this.Systems = new Dictionary<string, BaseSystem>();
            foreach (Type type in typeof(BaseSystem).GetSubClasses())
                this.Systems.Add(type.Name, (BaseSystem)Activator.CreateInstance(type, this));
            // TODO: RoomSystem/Manager - list of devices (different types - driver, sensor, camera; maybe subtype - motion, multi, light, switch)
            // TODO: SensorsSystem - get data from sensors devices in different rooms
            // TOOD: SecuritySystem - per room enablement and activation by room sensors
            // TODO: Trigger-Action system (migrate Schedule system to it - time trigger; sensor data alerts and light on skill also)

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
            // TODO: event?
            this.updateInterval = 0;

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
                return;
            }

            var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                Formatting = Formatting.Indented
            });

            var json = File.ReadAllText(Config.DataFilePath);
            var data = JObject.Parse(json);
            if (data.ContainsKey("LastBackupTime"))
                this.lastBackupTime = (DateTime)data["LastBackupTime"];
            if (data.ContainsKey(nameof(this.Config)))
                this.Config = data[nameof(this.Config)].ToObject<Config>(serializer);
            foreach (var kvp in data)
            {
                if (this.Systems.ContainsKey(kvp.Key))
                {
                    var reader = kvp.Value.CreateReader();
                    serializer.Populate(reader, this.Systems[kvp.Key]);
                }
            }

            this.SystemChanged = false;
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
                Formatting = Formatting.Indented
            });

            var data = new JObject
            {
                ["LastBackupTime"] = this.lastBackupTime,
                [nameof(this.Config)] = JToken.FromObject(this.Config, serializer)
            };
            foreach (var kvp in this.Systems)
                data.Add(kvp.Key, JToken.FromObject(kvp.Value, serializer));

            var json = data.ToString();
            File.WriteAllText(Config.DataFilePath, json);

            this.SystemChanged = false;
        }

        private void Update()
        {
            Stopwatch stopwatch = new Stopwatch();
            while (this.updateInterval > 0)
            {
                stopwatch.Restart();

                foreach (var system in this.Systems.Values)
                    system.Update(); // TODO: thread pool

                var now = DateTime.Now;
                if (now.Minute % this.upgradeCheckInterval == 0 && now.Second < this.updateInterval)
                {
                    // TODO: check for upgrade in thread pool task
                }

                if (this.SystemChanged)
                    this.Save();

                // TODO: logger.Debug(stopwatch.Elapsed);
                Thread.Sleep(this.updateInterval);
            }
        }

        public bool SendAlert(string msg, List<string> fileNames = null, bool force = false)
        {
            try
            {
                logger.Info($"Send alert {msg}");

                bool result = true;
                var deviceSystem = (DevicesSystem)this.Systems["DevicesSystem"];
                var latestData = deviceSystem.Sensors.ToDictionary(s => s.Name, s => s.LastValues);
                msg = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\n{msg}\n{JsonConvert.SerializeObject(latestData)}";

                // TODO: add camera images

                if (!Services.SendEMail(this.Config.SmtpServerAddress, this.Config.Email, this.Config.EmailPassword,
                    this.Config.Email, "My Home", msg, fileNames))
                {
                    result = false;
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
    }
}
