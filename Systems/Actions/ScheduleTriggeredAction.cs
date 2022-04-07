using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class ScheduleTriggeredAction : BaseAction
    {
        [UiProperty(true)]
        public TimeSpan Time { get; set; }

        [UiProperty(true, "If true Days will be day of the week, else of the month")]
        public bool UseDayOfWeek { get; set; }

        [UiProperty(true, "Day of the week(1=Monday)/month")]
        public List<int> Days { get; private set; }

        private bool triggered;


        public ScheduleTriggeredAction()
        {
            this.Time = TimeSpan.Zero;
            this.UseDayOfWeek = true;
            this.Days = new List<int>();

            this.triggered = false;
        }


        public override void Update()
        {
            base.Update();

            var now = DateTime.Now;
            if (this.UseDayOfWeek && !this.Days.Select(d => (DayOfWeek)(d % 7)).Contains(now.DayOfWeek))
                return;
            if (!this.UseDayOfWeek && !this.Days.Contains(now.Day))
                return;

            if (this.Time.Hours == now.Hour && this.Time.Minutes == now.Minute)
            {
                if (!this.triggered)
                    this.Trigger();
                this.triggered = true;
            }
            else
                this.triggered = false;
        }
    }
}