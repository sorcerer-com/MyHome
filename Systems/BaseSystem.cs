
using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public abstract class BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();


        public MyHome Owner { get; set; }

        [JsonIgnore]
        public string Name => this.GetType().Name[..^"System".Length];


        private BaseSystem() : this(null) { } // for json deserialization

        protected BaseSystem(MyHome owner)
        {
            this.Owner = owner;
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
