using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Utils;
using MyHome.Utils.Tuya;

using NLog;

namespace MyHome.Systems.Devices.Sensors
{
    // DPs from: https://github.com/jasonacox/tinytuya
    // for device key: https://sites.google.com/view/randyhomeassistant/tuya-local
    public class TuyaSensor : BaseSensor
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        [UiProperty(true)]
        public string TuyaDeviceId { get; set; }

        [UiProperty(true, "Android /data/data/com.tuya.smartlife/shared_prefs/preferences_global_xxxxxxxx.xml")]
        public string TuyaDeviceKey { get; set; }

        [UiProperty(true)]
        public string TuyaDeviceIp { get; set; }

        [UiProperty(true, "minutes")]
        public int ReadDataInterval { get; set; } // minutes


        private TuyaDevice tuyaDevice => new(this.TuyaDeviceIp, this.TuyaDeviceKey, this.TuyaDeviceId);

        private DateTime nextDataRead;


        public TuyaSensor()
        {
            this.TuyaDeviceId = "";
            this.TuyaDeviceKey = "";
            this.TuyaDeviceIp = "";
            this.ReadDataInterval = 5;

            this.nextDataRead = DateTime.Now.AddMinutes(1); // start reading 1 minute after start
        }


        public override void Update()
        {
            base.Update();

            // read sensor data
            if (DateTime.Now > this.nextDataRead)
            {
                logger.Trace($"Requesting data from {this.Name} ({this.Room.Name}) sensor");
                var data = this.ReadData();
                if (data != null)
                    this.AddData(DateTime.Now, data);
                else
                    logger.Warn($"No data from {this.Name} ({this.Room.Name}) sensor");
                this.nextDataRead = DateTime.Now.AddMinutes(this.ReadDataInterval);
            }
        }


        private Dictionary<string, object> ReadData()
        {
            try
            {
                var dps = this.tuyaDevice.GetDpsAsync().Result;
                return dps.Where(kvp => kvp.Value is bool || kvp.Value is long)
                    .ToDictionary(dp => dp.Key.ToString(), dp => dp.Value);
            }
            catch (Exception e)
            {
                logger.Error($"Cannot read Tuya device data {this.Name} ({this.Room.Name})");
                logger.Debug(e);
            }
            return null;
        }
    }
}
