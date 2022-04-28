﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Models
{
    public class Alert
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public enum AlertLevel
        {
            Low,
            Medium,
            Critical
        }

        private readonly string message;
        private string details;
        private AlertLevel level;
        private TimeSpan validty;
        private List<string> filenames;
        private bool camerasImages;
        private bool sensorsData;

        private static readonly Dictionary<string, DateTime> validities = new();

        private Alert(string message)
        {
            this.message = message;
            this.details = "";
            this.level = AlertLevel.Medium;
            this.validty = TimeSpan.Zero;
            this.filenames = new List<string>();
            this.camerasImages = true;
            this.sensorsData = true;
        }

        public static Alert Create(string message)
        {
            return new Alert(message);
        }

        public Alert Details(string details)
        {
            this.details = details;
            return this;
        }

        public Alert Level(AlertLevel level)
        {
            this.level = level;
            return this;
        }

        public Alert Validity(TimeSpan validty)
        {
            this.validty = validty;
            return this;
        }

        public Alert Filenames(List<string> filenames)
        {
            this.filenames = filenames.ToList();
            return this;
        }

        public Alert CamerasImages(bool camerasImages)
        {
            this.camerasImages = camerasImages;
            return this;
        }

        public Alert SensorsData(bool sensorsData)
        {
            this.sensorsData = sensorsData;
            return this;
        }

        public bool Send()
        {
            try
            {
                var msg = $"{this.message} {this.details}";
                if (!this.IsValid())
                    return true;
                logger.Info($"Send alert: {msg} ({this.level}, {this.validty}, {this.filenames.Count} files)");

                bool result = true;
                msg = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\n{msg}";

                var images = this.GetCameraImages();
                this.filenames.AddRange(images);

                var emailMsg = msg;
                if (this.sensorsData)
                {
                    var latestData = MyHome.Instance.DevicesSystem.Sensors.ToDictionary(s => s.Room.Name + "." + s.Name, s => s.Values);
                    emailMsg = $"{msg}\n\n{JsonConvert.SerializeObject(latestData, Formatting.Indented)}";
                }
                if (!Services.SendEMail(MyHome.Instance.Config.SmtpServerAddress, MyHome.Instance.Config.Email, MyHome.Instance.Config.EmailPassword,
                    MyHome.Instance.Config.Email, "My Home", emailMsg, this.filenames))
                {
                    result = false;
                }

                foreach (var file in images.Where(file => File.Exists(file)))
                    File.Delete(file);

                if (this.ShouldSendSMS() && !Services.SendSMS(MyHome.Instance.Config.GsmNumber, "telenor", MyHome.Instance.Config.MyTelenorPassword, msg))
                {
                    result = false;
                    Services.SendEMail(MyHome.Instance.Config.SmtpServerAddress, MyHome.Instance.Config.Email, MyHome.Instance.Config.EmailPassword,
                        MyHome.Instance.Config.Email, "My Home", "Alert sending failed");
                }
                return result;
            }
            catch (Exception e)
            {
                logger.Error("Cannot send alert message");
                logger.Debug(e);
                return false;
            }
        }


        private bool IsValid()
        {
            var result = false;
            if (!validities.ContainsKey(this.message) || DateTime.Now > validities[this.message])
                result = true;
            else
                logger.Trace($"Don't send new alert since there is valid alert until: {validities[this.message]}");

            if (result)
            {
                if (this.validty != TimeSpan.Zero)
                    validities[this.message] = DateTime.Now + this.validty;
                else
                    validities.Remove(this.message);
            }
            return result;
        }

        private List<string> GetCameraImages()
        {
            var images = new List<string>();
            if (!this.camerasImages)
                return images;

            foreach (var camera in MyHome.Instance.DevicesSystem.Cameras)
            {
                var filename = Path.Combine(Config.BinPath, $"{camera.Room.Name}_{camera.Name}.jpg");
                camera.SaveImage(filename);
                images.Add(filename);
            }
            return images;
        }

        private bool ShouldSendSMS()
        {
            if (this.level == AlertLevel.Critical)
                return true;

            var quietHours = MyHome.Instance.Config.QuietHours.Split("-", StringSplitOptions.TrimEntries).Select(h => int.Parse(h)).ToArray();

            var currHour = DateTime.Now.Hour;
            if ((quietHours[0] > quietHours[1] && (currHour > quietHours[0] || currHour < quietHours[1])) ||
                (quietHours[0] < quietHours[1] && (currHour > quietHours[0] && currHour < quietHours[1])))
            {
                logger.Debug($"Skip sending SMS since quiet hours: {quietHours[0]} - {quietHours[1]}");
                return false;
            }
            else if (this.level == AlertLevel.Low)
            {
                logger.Debug("Skip sending SMS since alert level is low");
                return false;
            }
            return true;
        }
    }
}