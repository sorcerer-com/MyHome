using System;
using System.Collections.Generic;
using System.Text;

using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;
using MyHome.Utils.Ewelink;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class EwelinkRfDriver : BaseDriver, ISwitchDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

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


        private static EwelinkV2 ewelink;
        private static EwelinkV2 Ewelink
        {
            get
            {
                if (ewelink == null)
                {
                    var password = Encoding.UTF8.GetString(Convert.FromBase64String(MyHome.Instance.Config.EwelinkPassword));
                    ewelink = new EwelinkV2(MyHome.Instance.Config.EwelinkCountryCode, MyHome.Instance.Config.EwelinkEmail, password);
                }
                return ewelink;
            }
        }

        // TODO: move to base Ewelink device?
        private static readonly Dictionary<string, DateTime> lastOnlineCache = new Dictionary<string, DateTime>();


        public EwelinkRfDriver()
        {
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
                        var dev = Ewelink.GetDevice(this.EwelinkDeviceId).Result;
                        if (dev?.Online == true)
                            lastOnlineCache[this.EwelinkDeviceId] = DateTime.Now;
                        logger.Trace($"Device '{this.EwelinkDeviceId}' online status: {dev?.Online}");
                    }
                    catch (Exception e)
                    {
                        logger.Trace(e, $"Failed to check device '{this.EwelinkDeviceId}' online status");
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
                logger.Info($"Transmit to eWeLink RF driver {this.Name} ({this.Room.Name})");
                Ewelink.TransmitRfChannel(this.EwelinkDeviceId, this.EwelinkRfChannel).Wait();
            }
            catch (Exception e)
            {
                logger.Error($"Cannot transmit RF channel '{this.EwelinkRfChannel}' to device: {this.Name} ({this.Room.Name})");
                logger.Debug(e);
                throw;
            }
        }
    }
}
