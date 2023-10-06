using System;

namespace MyHome.Utils
{
    public enum GlobalEventTypes
    {
        Start,
        Stop,
        Loaded,
        Saved,

        MediaPlayed,
        MediaStopped,
        MediaPaused,
        MediaVolumeDown,
        MediaVolumeUp,
        MediaSeekBack,
        MediaSeekForward,
        MediaSeekBackFast,
        MediaSeekForwardFast,

        SecurityAlarmActivated,
        PresenceChanged,

        SensorDataAdded,
        DriverStateChanged,

        AssistantResponse
    }

    public class GlobalEventArgs : EventArgs
    {
        public GlobalEventTypes EventType { get; }

        public object Data { get; }

        public GlobalEventArgs(GlobalEventTypes eventType, object data)
        {
            this.EventType = eventType;
            this.Data = data;
        }
    }

    public class GlobalEvent
    {
        public event EventHandler<GlobalEventArgs> Handler;

        public void Fire(object sender, GlobalEventTypes eventType)
        {
            this.Fire(sender, eventType, null);
        }

        public void Fire(object sender, GlobalEventTypes eventType, object data)
        {
            this.Handler?.Invoke(sender, new GlobalEventArgs(eventType, data));
        }
    }
}
