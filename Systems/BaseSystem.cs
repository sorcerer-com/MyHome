using MyHome.Utils;
using NLog;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyHome.Systems
{
    public abstract class BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        [JsonIgnore]
        public MyHome Owner { get; }

        public string Name => GetType().Name[..^"System".Length];


        public BaseSystem(MyHome owner)
        {
            Owner = owner;
            // TODO: ops synchronization
        }

        public virtual void Setup()
        {
            logger.Debug($"Setup system: {Name}");
        }

        public virtual void Stop()
        {
            logger.Debug($"Stop system: {Name}");
        }

        public virtual void Update()
        {
            //logger.Debug($"Update system: {Name}");
        }

        public virtual void Load(Dictionary<string, object> data)
        {
            this.SetJsonValues(data);
        }

        public virtual Dictionary<string, object> Save()
        {
            return this.GetJsonValues();
        }
    }
}
