using System.Text.RegularExpressions;

using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class ScriptDriver : BaseDriver
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        [UiProperty]
        public bool IsOn
        {
            get => false;
            set
            {
                if (value)
                    this.Execute();
            }
        }

        [UiProperty(true, code: true)]
        public string Script { get; set; }

        [UiProperty(true)]
        public bool ConfirmationRequired { get; set; }


        public ScriptDriver()
        {
            // TODO: whole driver just for one button?
        }


        public void Execute()
        {
            if (MyHome.Instance.BackupMode)
                return;

            var script = Regex.Replace(this.Script, @" as \w*", ""); // remove " as <Type>" casts
            logger.Trace($"Execute script: {script}");
            MyHome.Instance.ExecuteJint(jint => jint.Evaluate($"{{ {script} }}"),
                $"execute driver '{this.Name}' ({this.Room.Name}) script:\n{script}");
        }
    }
}
