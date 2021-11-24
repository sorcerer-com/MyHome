
using MyHome.Systems.Actions.Conditions;
using MyHome.Systems.Actions.Executors;
using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Actions
{
    public abstract class BaseAction
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public string Name { get; set; }

        [UiProperty(true)]
        public bool IsEnabled { get; set; }

        // TODO: multiple conditions and executors

        [UiProperty(true)]
        public BaseCondition ActionCondition { get; set; }

        [UiProperty(true)]
        public BaseExecutor Executor { get; set; }


        protected BaseAction()
        {
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
            {
                if (this.ActionCondition?.Check() != false) // if there is no condition or it's true
                    this.Executor?.Execute();
                else
                    logger.Debug("Action condition doesn't met");
            }
            else
                logger.Debug("Action is disabled");
        }
    }
}
