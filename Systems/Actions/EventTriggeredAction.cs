using System.Collections.Generic;
using System.Linq;

using MyHome.Models;
using MyHome.Systems.Actions.Executors;
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
            set => this.eventRoom = this.Owner.Owner.Rooms.FirstOrDefault(r => r.Name == value);
        }

        [UiProperty(true)]
        public GlobalEventTypes EventType { get; set; }


        private EventTriggeredAction() : this(null, null, true, null, null, GlobalEventTypes.Start) { }  // for json deserialization

        public EventTriggeredAction(ActionsSystem owner, string name, bool isEnabled, BaseExecutor executor,
            Room eventRoom, GlobalEventTypes eventType) :
            base(owner, name, isEnabled, executor)
        {
            this.eventRoom = eventRoom;
            this.EventType = eventType;
        }


        public override void Setup()
        {
            base.Setup();

            this.Owner.Owner.Events.Handler += this.Events_Handler;
        }

        public IEnumerable<string> GetRooms() // EventRoomName selector
        {
            return this.Owner.Owner.Rooms.Select(r => r.Name);
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
