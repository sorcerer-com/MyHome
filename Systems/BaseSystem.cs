
using System;
using System.Diagnostics;
using System.Threading;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public abstract class BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private int updateInterval = 1; // seconds


        [JsonIgnore]
        public string Name => this.GetType().Name[..^"System".Length];


        protected BaseSystem()
        {
        }


        public virtual void Setup()
        {
            logger.Debug($"Setup system: {this.Name}");
            var thread = new Thread(this.Loop)
            {
                Name = $"{this.Name} Update",
                IsBackground = true
            };
            thread.Start();
        }

        public virtual void Stop()
        {
            logger.Debug($"Stop system: {this.Name}");
            this.updateInterval = 0;
        }


        protected virtual void Update()
        {
            //logger.Debug($"Update system: {Name}");
        }


        private void Loop()
        {
            Stopwatch stopwatch = new Stopwatch();
            while (this.updateInterval > 0)
            {
                stopwatch.Restart();

                this.Update();

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(this.updateInterval))
                {
                    logger.Trace($"{this.Name} system update time: {stopwatch.Elapsed}");
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromSeconds(this.updateInterval) - stopwatch.Elapsed);
                }
            }
        }
    }
}
