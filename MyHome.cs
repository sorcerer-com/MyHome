using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

using MyHome.Systems;
using MyHome.Utils;

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

        public Config Config { get; }

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
            // TODO: Trigger-Action system (migrate Schedule system to it - time trigger; light on skill also)

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

        public void Load()
        {
            logger.Info("Load settings and data");
            if (!File.Exists(Config.DataFilePath))
            {
                logger.Warn("Data file doesn't exist");
                return;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.Converters.Add(new GenericJsonConverter());

            var json = File.ReadAllText(Config.DataFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
            if (data.ContainsKey("LastBackupTime"))
                this.lastBackupTime = (DateTime)data["LastBackupTime"];
            if (data.ContainsKey("Config"))
                this.Config.Load((Dictionary<string, object>)data["Config"]);
            foreach (var system in this.Systems.Values)
            {
                if (data.ContainsKey(system.Name))
                    system.Load((Dictionary<string, object>)data[system.Name]);
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
                File.Copy(Config.DataFilePath, Config.DataFilePath + ".bak");
            }

            var data = new Dictionary<string, object>
            {
                { "LastBackupTime", this.lastBackupTime },
                { "Config", this.Config.Save() }
            };
            foreach (var system in this.Systems.Values)
                data.Add(system.Name, system.Save());

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.Converters.Add(new GenericJsonConverter());
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(Config.DataFilePath, json);

            this.SystemChanged = false;
        }
    }
}
