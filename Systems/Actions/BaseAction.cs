using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

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

        [JsonProperty]
        private Room targetRoom;

        [JsonIgnore]
        [UiProperty(true, selector: "GetRooms")]
        public string TargetRoomName
        {
            get => this.targetRoom?.Name ?? "";
            set => this.targetRoom = MyHome.Instance.Rooms.FirstOrDefault(r => r.Name == value);
        }

        [UiProperty]
        public string Script { get; set; }


        protected BaseAction()
        {
        }


        public virtual void Setup()
        {
            logger.Debug($"Setup action: {this.Name}");
            if (string.IsNullOrEmpty(this.Script))
                logger.Warn($"Action '{this.Name}' script is empty");
        }

        public virtual void Update()
        {
        }

        public Task<bool> Trigger(bool asyncExec = true)
        {
            logger.Trace($"Action triggered: {this.Name}");
            if (this.IsEnabled)
            {
                var script = Regex.Replace(this.Script, @" as \w*", ""); // remove " as <Type>" casts
                if (asyncExec)
                    return Task.Run(() => this.ExecuteScript(script));
                else if (!this.ExecuteScript(script))
                    return Task.FromResult(false);
            }
            else
                logger.Trace("Action is disabled");
            return Task.FromResult(true);
        }


        private bool ExecuteScript(string script)
        {
            logger.Trace($"Execute script: {script}");
            // TODO: send alert if failed?
            return MyHome.Instance.ExecuteJint(jint => jint
                        .SetValue("logger", logger)
                        .SetValue("myHome", MyHome.Instance)
                        .SetValue("Rooms", MyHome.Instance.Rooms.ToDictionary(r => r.Name.Replace(" ", "")))
                        .SetValue("Devices", MyHome.Instance.Rooms.ToDictionary(r => r.Name.Replace(" ", ""),
                                                    r => r.Devices.ToDictionary(d => d.Name.Replace(" ", ""))))
                        .Evaluate($"{{ {script} }}"),
                        $"execute action '{this.Name}' script:\n{script}");
        }
    }
}
