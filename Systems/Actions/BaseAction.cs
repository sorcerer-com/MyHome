
using MyHome.Systems.Actions.Executors;
using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Actions
{
    public abstract class BaseAction
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public ActionsSystem Owner { get; set; }

        [UiProperty(true)]
        public string Name { get; set; }

        [UiProperty(true)]
        public bool IsEnabled { get; set; }

        // TODO: add conditions

        [UiProperty(true)]
        public BaseExecutor Executor { get; set; }


        private BaseAction() : this(null, null, true, null) { }  // for json deserialization

        protected BaseAction(ActionsSystem owner, string name, bool isEnabled, BaseExecutor executor)
        {
            this.Owner = owner;
            this.Name = name;
            this.IsEnabled = isEnabled;
            this.Executor = executor;
        }


        public virtual void Setup()
        {
            logger.Debug($"Setup action: {this.Name}");
            if (this.Executor == null)
                logger.Warn($"Action '{this.Name}' hasn't executor");
        }

        public virtual void Update()
        {
        }

        protected void Trigger()
        {
            logger.Debug($"Action triggered: {this.Name}");
            if (this.IsEnabled)
                this.Executor?.Execute();
            else
                logger.Debug("Action is disabled");
        }
    }
}
