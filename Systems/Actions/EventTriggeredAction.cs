using System.Linq;
using System.Threading.Tasks;

using MyHome.Models;
using MyHome.Systems.Devices;
using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Systems.Actions
{
    public class EventTriggeredAction : BaseAction
    {
        [JsonProperty]
        protected Device device;

        [JsonIgnore]
        [UiProperty(true, selector: "GetDevices")]
        public string DeviceName
        {
            get => this.device != null ? $"{this.device.Room.Name}.{this.device.Name}" : "";
            set
            {
                var split = value?.Split('.');
                if (split?.Length == 2)
                    this.device = MyHome.Instance.Rooms.Find(r => r.Name == split[0])?.Devices.FirstOrDefault(s => s.Name == split[1]);
            }
        }

        [UiProperty(true)]
        public GlobalEventTypes EventType { get; set; }

        [UiProperty(true)]
        public string EventData { get; set; }


        public EventTriggeredAction()
        {
        }


        public override void Setup()
        {
            base.Setup();

            MyHome.Instance.Events.Handler += this.Events_Handler;
        }

        private void Events_Handler(object sender, GlobalEventArgs e)
        {
            if (this.IsTriggered(sender, e))
                Task.Run(this.Trigger);
        }

        protected virtual bool IsTriggered(object sender, GlobalEventArgs e)
        {
            if (e.EventType != this.EventType)
                return false;

            if (this.device != null && sender != this.device)
                return false;

            var room = (Room)sender.GetType().GetProperty("Room")?.GetValue(sender);
            if (this.targetRoom != null && room != this.targetRoom)
                return false;

            if (!string.IsNullOrEmpty(this.EventData) && !e.Data?.Equals(Utils.Utils.ParseValue(this.EventData, e.Data.GetType())))
                return false;

            return true;
        }
    }
}
