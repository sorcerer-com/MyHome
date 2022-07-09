using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using MyHome.Utils;

namespace MyHome.Systems.Actions
{
    public class ScheduleTriggeredAction : BaseAction
    {
        [UiProperty(true, "Time of day if SolarTime is not set, else offset")]
        public TimeSpan Time { get; set; }

        [UiProperty(true, "If set, Time is used as offset (should not exceed the day)", "GetSolarTimes")]
        public string SolarTime { get; set; }

        [UiProperty(true, "If true Days will be day of the week, else of the month")]
        public bool UseDayOfWeek { get; set; }

        [UiProperty(true, "Day of the week(1=Monday)/month. If empty everyday.")]
        public List<int> Days { get; private set; }


        private bool triggered;
        private DateTime lastSolarDate;
        private TimeSpan solarTime;


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
            if (this.Days.Count > 0) // if Days is empty - everyday
            {
                if (this.UseDayOfWeek && !this.Days.Select(d => (DayOfWeek)(d % 7)).Contains(now.DayOfWeek))
                    return;
                if (!this.UseDayOfWeek && !this.Days.Contains(now.Day))
                    return;
            }

            var time = this.Time;
            if (!string.IsNullOrEmpty(this.SolarTime))
                time += this.GetSolarTime();

            if (time.Hours == now.Hour && time.Minutes == now.Minute)
            {
                if (!this.triggered)
                    this.Trigger();
                this.triggered = true;
            }
            else
                this.triggered = false;
        }

        private TimeSpan GetSolarTime()
        {
            var now = DateTime.Now;
            if (this.lastSolarDate != now.Date) // cache value every day
            {
                var (lat, @long) = MyHome.Instance.Config.Location;
                var solarTimes = new SolarTimes(now.Date, lat, @long);
                var time = (DateTime)typeof(SolarTimes).GetProperty(this.SolarTime).GetValue(solarTimes);
                this.solarTime = time.TimeOfDay;
                this.lastSolarDate = now.Date;
            }
            return this.solarTime;
        }


        public static IEnumerable<(string, string)> GetSolarTimes()
        {
            return typeof(SolarTimes)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(pi => pi.PropertyType == typeof(DateTime))
                .Select(pi => (pi.Name, pi.Name));
        }
    }
}