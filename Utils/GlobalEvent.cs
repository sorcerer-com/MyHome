using System;

namespace MyHome.Utils
{
    public class GlobalEvent
    {
        public class GlobalEventArgs : EventArgs
        {
            public string EventType { get; }

            public object Data { get; }

            public GlobalEventArgs(string eventType, object data)
            {
                this.EventType = eventType;
                this.Data = data;
            }
        }

        public event EventHandler<GlobalEventArgs> Handler;


        public void Fire(object sender, string eventType)
        {
            this.Fire(sender, eventType, null);
        }

        public void Fire(object sender, string eventType, object data)
        {
            this.Handler?.Invoke(sender, new GlobalEventArgs(eventType, data));
        }
    }
}
