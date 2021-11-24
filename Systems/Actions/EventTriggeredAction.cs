using System.Linq;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Systems.Actions
{
    public class EventTriggeredAction : BaseAction
    {
        [JsonProperty]
        private Room eventRoom;

        [JsonIgnore]
        [UiProperty(true, selector: "GetRooms")]
        public string EventRoomName
        {
            get => this.eventRoom?.Name ?? "";
            set => this.eventRoom = MyHome.Instance.Rooms.FirstOrDefault(r => r.Name == value);
        }

        [UiProperty(true)]
        public GlobalEventTypes EventType { get; set; }


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
                this.Trigger();
        }

        protected virtual bool IsTriggered(object sender, GlobalEventArgs e)
        {
            if (e.EventType != this.EventType)
                return false;

            var room = (Room)sender.GetType().GetProperty("Room")?.GetValue(sender);
            if (room != this.eventRoom && this.eventRoom != null)
                return false;

            return true;
        }
    }
}
