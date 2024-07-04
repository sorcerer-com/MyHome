
using System;
using System.Diagnostics;
using System.Threading;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public abstract class BaseSystem
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

                try
                {
                    this.Update();
                }
                catch (Exception e)
                {
                    logger.Error($"Failed to update system: {this.Name}");
                    logger.Debug(e);
                }

                var delta = TimeSpan.FromSeconds(this.updateInterval) - stopwatch.Elapsed;
                if (delta.TotalMilliseconds <= 0) // update take longer than set interval
                    logger.Trace($"{this.Name} system update time: {stopwatch.Elapsed}");
                else
                    Thread.Sleep(Math.Max(0, (int)delta.TotalMilliseconds));
            }
        }
    }
}
