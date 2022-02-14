using System;
using System.Collections.Generic;

using MyHome.Utils;
using MyHome.Utils.Ewelink;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems.Devices.Drivers
{
    public class EwelinkRfDriver : BaseDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public string EwelinkEmail { get; set; }

        [UiProperty(true)]
        public string EwelinkPassword { get; set; }

        [UiProperty(true)]
        public string EwelinkDeviceId { get; set; }

        [UiProperty(true)]
        public int EwelinkRfChannel { get; set; }

        [JsonIgnore]
        [UiProperty]
        public bool IsOn
        {
            get => false;
            set => this.TransmitRfDriver(value);
        }

        [UiProperty(true)]
        public bool ConfirmationRequired { get; set; }


        public EwelinkRfDriver()
        {
        }


        private void TransmitRfDriver(bool value)
        {
            if (value)
            {
                try
                {
                    var ewelink = new Ewelink(this.EwelinkEmail, this.EwelinkPassword);
                    ewelink.TransmitRfChannel(this.EwelinkDeviceId, this.EwelinkRfChannel).Wait();
                }
                catch (Exception e)
                {
                    logger.Error($"Cannot transmit RF channel '{this.EwelinkRfChannel}' to device: {this.Name} ({this.Room.Name})");
                    logger.Debug(e);
                }
            }
        }
    }
}
