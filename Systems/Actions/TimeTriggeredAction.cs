using System;

using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class TimeTriggeredAction : BaseAction
    {
        // should be defined before time, to calculate it correctly
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


        public TimeTriggeredAction()
        {
        }


        public override void Update()
        {
            base.Update();

            if (DateTime.Now < this.time)
                return;

            this.Trigger();

            while (this.time < DateTime.Now)
                this.time += this.interval;
        }
    }
}
