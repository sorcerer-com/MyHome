using System;

using MyHome.Models;
using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class TimeTriggeredAction : BaseAction
    {
        // should be defined before time, to calculate it corrently
        private TimeSpan interval;
        [UiProperty(true)]
        public string Interval
        {
            get => this.interval.ToString();
            set
            {
                _ = TimeSpan.TryParse(value, out this.interval);
                if (this.interval.TotalMilliseconds == 0)
                    this.interval = TimeSpan.FromHours(1);
            }
        }

        private DateTime time;
        [UiProperty(true)]
        public DateTime Time
        {
            get => this.time;
            set
            {
                this.time = value;

                while (this.time < DateTime.Now)
                    this.time += this.interval;
            }
        }


        private TimeTriggeredAction() : this(null, null, null, DateTime.Now, TimeSpan.FromHours(1)) { }  // for json deserialization

        public TimeTriggeredAction(ActionsSystem owner, Room room, string action, DateTime time, TimeSpan interval) : base(owner, room, action)
        {
            this.Interval = interval.ToString();
            this.Time = time; // first set interval to calculate nextTime correctly
        }


        public override void Update()
        {
            base.Update();

            if (DateTime.Now < this.time)
                return;

            this.Execute();

            while (this.time < DateTime.Now)
                this.time += this.interval;
        }
    }
}
