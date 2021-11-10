﻿using System.Collections.Generic;
using System.Linq;

using MyHome.Models;
using MyHome.Systems.Devices;
using MyHome.Utils;

namespace MyHome.Systems.Actions.Executors
{
    public abstract class BaseExecutor
    {
        protected Room Room
        {
            get
            {
                var target = this.Target[0..this.Target.LastIndexOf(' ')]; // remove target type
                var split = target.Split(".");
                return MyHome.Instance.Rooms.FirstOrDefault(r => r.Name == split[0]);
            }
        }

        protected Device Device
        {
            get
            {
                var target = this.Target[0..this.Target.LastIndexOf(' ')]; // remove target type
                var split = target.Split(".");
                if (split.Length > 1)
                    return this.Room?.Devices.FirstOrDefault(d => d.Name == split[1]);
                return null;
            }
        }

        [UiProperty(true, selector: "GetTarget")]
        public string Target { get; set; }


        protected BaseExecutor()
        {
        }

        public abstract void Execute();

        public IEnumerable<string> GetTarget() // Target selector
        {
            var targets = MyHome.Instance.Rooms.Select(r => $"{r.Name} ({r.GetType().Name})");
            targets = targets.Union(
                MyHome.Instance.Rooms.SelectMany(r =>
                    r.Devices.Select(d => $"{r.Name}.{d.Name} ({d.GetType().Name})")));
            return targets;
        }
    }
}
