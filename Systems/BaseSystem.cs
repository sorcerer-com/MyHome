using System.Collections.Generic;
using System.Text.Json.Serialization;

using MyHome.Utils;

using NLog;

namespace MyHome.Systems
{
    public abstract class BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        [JsonIgnore]
        public MyHome Owner { get; }

        public string Name => this.GetType().Name[..^"System".Length];


        public BaseSystem(MyHome owner)
        {
            this.Owner = owner;
            // TODO: ops synchronization
        }

        public virtual void Setup()
        {
            logger.Debug($"Setup system: {this.Name}");
        }

        public virtual void Stop()
        {
            logger.Debug($"Stop system: {this.Name}");
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
