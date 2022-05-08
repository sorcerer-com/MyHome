﻿using System.Linq;
using System.Text.RegularExpressions;

using MyHome.Utils;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class ScriptDriver : BaseDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

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
            var script = Regex.Replace(this.Script, @" as \w*", ""); // remove " as <Type>" casts
            logger.Trace($"Execute script: {script}");
            MyHome.Instance.ExecuteJint(jint => jint
                    .SetValue("logger", logger)
                    .SetValue("myHome", MyHome.Instance)
                    .SetValue("Rooms", MyHome.Instance.Rooms.ToDictionary(r => r.Name.Replace(" ", "")))
                    .SetValue("Devices", MyHome.Instance.Rooms.ToDictionary(r => r.Name.Replace(" ", ""),
                                         r => r.Devices.ToDictionary(d => d.Name.Replace(" ", ""))))
                    .Evaluate($"{{ {script} }}"),
                    $"execute driver '{this.Name}' ({this.Room.Name}) script:\n{script}");
        }
    }
}
