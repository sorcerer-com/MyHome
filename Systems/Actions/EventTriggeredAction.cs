
using MyHome.Models;

namespace MyHome.Systems.Actions
{
    public class EventTriggeredAction : BaseAction
    {
        public Room TriggerRoom { get; set; }

        public string TriggerEvent { get; set; } // TODO: change event type to enum

        public EventTriggeredAction(ActionsSystem owner, Room triggerRoom, string triggerEvent, Room room, string action) :
            base(owner, room, action)
        {
            this.TriggerRoom = triggerRoom;
            this.TriggerEvent = triggerEvent;
        }

        public override void Setup()
        {
            base.Setup();

            this.Owner.Owner.Events.Handler += this.Events_Handler;
        }

        protected virtual bool IsTriggered(object sender, GlobalEvent.GlobalEventArgs e)
        {
            if (e.EventType != this.TriggerEvent)
                return false;

            var room = (Room)sender.GetType().GetProperty("Room")?.GetValue(sender);
            if (room != this.TriggerRoom && this.TriggerRoom != null)
                return false;

            return true;
        }

        private void Events_Handler(object sender, GlobalEvent.GlobalEventArgs e)
        {
            if (this.IsTriggered(sender, e))
                this.Execute();
        }
    }
}
