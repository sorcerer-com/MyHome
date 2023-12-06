using System;
using System.Collections.Generic;

using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;
using MyHome.Utils.Ewelink;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class EwelinkRfDriver : BaseDriver, ISwitchDriver
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public override DateTime LastOnline => !string.IsNullOrEmpty(this.EwelinkDeviceId) && lastOnlineCache.ContainsKey(this.EwelinkDeviceId)
            ? lastOnlineCache[this.EwelinkDeviceId] : DateTime.Now;


        [UiProperty(true)]
        public string EwelinkDeviceId { get; set; }

        [UiProperty(true)]
        public int EwelinkRfChannel { get; set; }

        [JsonIgnore]
        [UiProperty]
        public bool IsOn
        {
            get => false;
            set
            {
                if (value)
                    this.TransmitRfDriver();
            }
        }

        [UiProperty(true)]
        public bool ConfirmationRequired { get; set; }

        private EwelinkV2 Ewelink => MyHome.Instance.DevicesSystem.Ewelink;

        // TODO: move to base Ewelink device?
        private static readonly Dictionary<string, DateTime> lastOnlineCache = new();


        public EwelinkRfDriver()
        {
            this.EwelinkDeviceId = "";
            this.EwelinkRfChannel = 0;
        }

        public override void Update()
        {
            base.Update();

            lock (lastOnlineCache)
            {
                if (!lastOnlineCache.ContainsKey(this.EwelinkDeviceId))
                    lastOnlineCache[this.EwelinkDeviceId] = DateTime.Now;
                var minutes = (DateTime.Now - lastOnlineCache[this.EwelinkDeviceId]).Minutes;
                if (minutes >= 5 && minutes % 5 == 0) // refresh online status every 5 minutes
                {
                    lastOnlineCache[this.EwelinkDeviceId] -= TimeSpan.FromMinutes(1);
                    try
                    {
                        var dev = this.Ewelink.GetDevice(this.EwelinkDeviceId).Result;
                        if (dev?.Online == true)
                            lastOnlineCache[this.EwelinkDeviceId] = DateTime.Now;
                        logger.Trace($"EWeLink device '{this.EwelinkDeviceId}' ({this.Name}, {this.Room.Name}) online status: {dev?.Online}");
                    }
                    catch (Exception e)
                    {
                        logger.Trace(e, $"Failed to check eWeLink device '{this.EwelinkDeviceId}' ({this.Name}, {this.Room.Name}) online status");
                    }
                }
            }
        }

        public bool Toggle()
        {
            this.IsOn = !this.IsOn;
            return this.IsOn;
        }


        private void TransmitRfDriver()
        {
            try
            {
                logger.Info($"Transmit to channel {this.EwelinkRfChannel} of eWeLink RF driver {this.Name} ({this.Room.Name})");
                this.Ewelink.TransmitRfChannel(this.EwelinkDeviceId, this.EwelinkRfChannel).Wait();
            }
            catch (Exception e)
            {
                logger.Error($"Cannot transmit to RF channel '{this.EwelinkRfChannel}' of device {this.Name} ({this.Room.Name})");
                logger.Debug(e);
                throw;
            }
        }
    }
}
