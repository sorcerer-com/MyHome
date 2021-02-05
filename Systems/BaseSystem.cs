
using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public abstract class BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        [JsonIgnore]
        public MyHome Owner { get; }

        [JsonIgnore]
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
    }
}
