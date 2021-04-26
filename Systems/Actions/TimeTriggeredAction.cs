using System;

using MyHome.Models;

namespace MyHome.Systems.Actions
{
    public class TimeTriggeredAction : BaseAction
    {
        private TimeSpan time;
        public TimeSpan Time
        {
            get => this.time;
            set
            {
                this.time = value;

                var now = DateTime.Now;
                this.nextTime = now - now.TimeOfDay + this.time;
                while (this.nextTime < DateTime.Now)
                    this.nextTime += this.Interval;
            }
        }

        public TimeSpan Interval { get; set; }

        private DateTime nextTime;


        public TimeTriggeredAction(ActionsSystem owner, Room room, string action, TimeSpan time, TimeSpan interval) : base(owner, room, action)
        {
            this.Interval = interval;
            this.Time = time; // first set interval to calculate nextTime correctly
        }

        public override void Update()
        {
            base.Update();

            if (DateTime.Now < this.nextTime)
                return;

            this.Execute();

            while (this.nextTime < DateTime.Now)
                this.nextTime += this.Interval;
        }
    }
}
