using System;
using System.Collections.Generic;

using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;
using MyHome.Utils.Tuya;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class TuyaSwitchDriver : BaseDriver, ISwitchDriver
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public override DateTime LastOnline => lastOnlineCache.ContainsKey(this.TuyaDeviceId) ? lastOnlineCache[this.TuyaDeviceId] : DateTime.Now;


        [UiProperty(true)]
        public string TuyaDeviceId { get; set; }

        [UiProperty(true, "Android /data/data/com.tuya.smartlife/shared_prefs/preferences_global_xxxxxxxx.xml")]
        public string TuyaDeviceKey { get; set; }

        [UiProperty(true)]
        public string TuyaDeviceIp { get; set; }

        [UiProperty(true)]
        public int TuyaSwitchIdx { get; set; }

        private bool isOn;

        [JsonIgnore]
        [UiProperty]
        public bool IsOn
        {
            get => this.isOn;
            set => this.SetIsOn(value);
        }

        [UiProperty(true)]
        public bool ConfirmationRequired { get; set; }


        private TuyaDevice tuyaDevice => new(this.TuyaDeviceIp, this.TuyaDeviceKey, this.TuyaDeviceId);

        // TODO: move to base Tuya device?
        private static readonly Dictionary<string, DateTime> lastOnlineCache = new();


        public TuyaSwitchDriver()
        {
            this.TuyaDeviceId = "";
            this.TuyaDeviceKey = "";
            this.TuyaDeviceIp = "";
            this.TuyaSwitchIdx = 1;
        }

        public override void Update()
        {
            base.Update();

            lock (lastOnlineCache)
            {
                if (!lastOnlineCache.ContainsKey(this.TuyaDeviceId))
                    lastOnlineCache[this.TuyaDeviceId] = DateTime.Now - TimeSpan.FromMinutes(1);
                var minutes = (DateTime.Now - lastOnlineCache[this.TuyaDeviceId]).Minutes;
                if (minutes >= 1) // refresh status every minute
                {
                    lastOnlineCache[this.TuyaDeviceId] -= TimeSpan.FromMinutes(0.5);
                    try
                    {
                        var dps = this.tuyaDevice.GetDpsAsync().Result;
                        var dp = Math.Max(1, Math.Min(4, this.TuyaSwitchIdx));
                        this.isOn = (bool)dps[dp];
                        logger.Trace($"Tuya device '{this.TuyaDeviceId}' ({this.Name}, {this.Room.Name}) is online");
                    }
                    catch (Exception e)
                    {
                        logger.Trace(e, $"Failed to check Tuya device '{this.TuyaDeviceId}' ({this.Name}, {this.Room.Name}) online status");
                    }
                    lastOnlineCache[this.TuyaDeviceId] = DateTime.Now;
                }
            }
        }

        public bool Toggle()
        {
            this.IsOn = !this.IsOn;
            return this.IsOn;
        }

        private void SetIsOn(bool value)
        {
            if (MyHome.Instance.BackupMode.Enabled || 
                this.isOn == value)
                return;

            var dp = Math.Max(1, Math.Min(4, this.TuyaSwitchIdx));
            try
            {
                logger.Info($"Set value '{value}' to dp {dp} of Tuya switch driver {this.Name} ({this.Room.Name})");
                this.tuyaDevice.SetDpAsync(dp, value).Wait();
                var dps = this.tuyaDevice.GetDpsAsync().Result;
                this.isOn = (bool)dps[dp];
            }
            catch (Exception e)
            {
                logger.Error($"Cannot set value '{value}' to dp {dp} of Tuya switch driver {this.Name} ({this.Room.Name})");
                logger.Debug(e);
                throw;
            }
        }
    }
}
