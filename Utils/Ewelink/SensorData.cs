﻿namespace MyHome.Utils.Ewelink
{
    public class SensorData
    {
        public SensorData(string deviceId, SensorType type, double value)
        {
            this.DeviceId = deviceId;
            this.Type = type;
            this.Value = value;
        }

        public string DeviceId { get; }

        public SensorType Type { get; }

        public double Value { get; }
    }
}