using MyHome.Systems;
using MyHome.Utils;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

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

            Config = new Config();

            Systems = new Dictionary<string, BaseSystem>();
            foreach (Type type in typeof(BaseSystem).GetSubClasses())
                Systems.Add(type.Name, (BaseSystem)Activator.CreateInstance(type, this));
            // TODO: Trigger-Action system (migrate Schedule system to it - time trigger; light on skill also)

            lastBackupTime = DateTime.Now;
            SystemChanged = false;

            Load();

            Setup();

            thread = new Thread(Update)
            {
                Name = "MyHome update",
                IsBackground = true
            };
            thread.Start();
        }

        void IDisposable.Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        public void Setup()
        {
            logger.Info("Setup My Home");
            foreach (var system in Systems.Values)
                system.Setup();
        }

        public void Stop()
        {
            // TODO: event?
            updateInterval = 0;

            Save();
            foreach (var system in Systems.Values)
                system.Stop();
            logger.Info("Stop My Home");
        }

        private void Update()
        {
            Stopwatch stopwatch = new Stopwatch();
            while (updateInterval > 0)
            {
                stopwatch.Restart();

                foreach (var system in Systems.Values)
                    system.Update(); // TODO: thread pool

                var now = DateTime.Now;
                if (now.Minute % upgradeCheckInterval == 0 && now.Second < updateInterval)
                {
                    // TODO: check for upgrade in thread pool task
                }

                if (SystemChanged)
                    Save();

                // TODO: logger.Debug(stopwatch.Elapsed);
                Thread.Sleep(updateInterval);
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
                lastBackupTime = (DateTime)data["LastBackupTime"];
            if (data.ContainsKey("Config"))
                Config.Load((Dictionary<string, object>)data["Config"]);
            foreach (var system in Systems.Values)
            {
                if (data.ContainsKey(system.Name))
                    system.Load((Dictionary<string, object>)data[system.Name]);
            }

            SystemChanged = false;
        }

        public void Save()
        {
            logger.Info("Save settings and data");

            // backup data file every day
            if (DateTime.Now - lastBackupTime > TimeSpan.FromDays(1))
            {
                lastBackupTime = DateTime.Now;
                File.Copy(Config.DataFilePath, Config.DataFilePath + ".bak");
            }

            var data = new Dictionary<string, object>
            {
                { "LastBackupTime", lastBackupTime },
                { "Config", Config.Save() }
            };
            foreach (var system in Systems.Values)
                data.Add(system.Name, system.Save());

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            options.Converters.Add(new GenericJsonConverter());
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(Config.DataFilePath, json);

            SystemChanged = false;
        }
    }
}
