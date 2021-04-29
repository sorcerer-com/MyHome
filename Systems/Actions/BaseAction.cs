using System;
using System.Linq;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Actions
{
    public enum Condition
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    public abstract class BaseAction
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public ActionsSystem Owner { get; set; }

        public Room Room { get; set; }

        [JsonIgnore]
        [UiProperty(true)]
        public string RoomName
        {
            get => this.Room?.Name ?? "";
            set => this.Room = this.Owner.Owner.Rooms.FirstOrDefault(r => r.Name == value);
        }

        [UiProperty(true, "[device.]prop = value / [device.]func()")]
        public string Action { get; set; }


        private BaseAction() : this(null, null, null) { }  // for json deserialization

        public BaseAction(ActionsSystem owner, Room room, string action)
        {
            this.Owner = owner;
            this.Room = room;
            this.Action = action;
        }


        public virtual void Setup()
        {
        }

        public virtual void Update()
        {
        }


        public void Execute()
        {
            logger.Debug("Action triggered: " + this.Owner.Actions.FirstOrDefault(kvp => kvp.Value == this).Key);

            if (this.Action.Contains("="))
            {
                var splitValue = this.Action.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (splitValue[0].Contains(".")) // deviceName.property = value
                {
                    var splitDevice = splitValue[0].Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var device = this.Room.Devices.FirstOrDefault(d => d.Name == splitDevice[0]);
                    if (device == null)
                        logger.Error($"Action try to execute on invalid device: {splitDevice[0]} ({this.Room.Name})");
                    else
                        SetProperty(device, splitDevice[1], splitValue[1]);
                }
                else // room: property = value
                {
                    SetProperty(this.Room, splitValue[0], splitValue[1]);
                }
            }
            else if (this.Action.EndsWith("()"))
            {
                var action = this.Action[..^2];
                if (action.Contains(".")) // deviceName.function()
                {
                    var splitDevice = action.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var device = this.Room.Devices.FirstOrDefault(d => d.Name == splitDevice[0]);
                    if (device == null)
                        logger.Error($"Action try to execute on invalid device: {splitDevice[0]} ({this.Room.Name})");
                    else
                        CallMethod(device, splitDevice[1]);
                }
                else // room: function()
                {
                    CallMethod(this.Room, action);
                }
            }
            else
            {
                logger.Error("Invalid action: " + this.Action);
            }
        }

        private static void SetProperty(object obj, string property, string value)
        {
            var prop = obj.GetType().GetProperty(property);
            if (prop == null)
            {
                logger.Error($"Action try to set value on invalid property: {property}");
                return;
            }

            try
            {
                prop.SetValue(obj, Utils.Utils.ParseValue(value, prop.PropertyType));
            }
            catch (Exception e)
            {
                logger.Error(e, "Action failed to set property value");
            }
        }

        private static void CallMethod(object obj, string methodName)
        {
            var method = obj.GetType().GetMethod(methodName);
            if (method == null)
            {
                logger.Error($"Action try to call invalid method: {methodName}");
                return;
            }

            try
            {
                method.Invoke(obj, null);
            }
            catch (Exception e)
            {
                logger.Error(e, "Action failed to call method");
            }
        }
    }
}
