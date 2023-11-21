using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
        private TimeSpan validity;
        private List<string> filenames;
        private bool camerasImages;
        private bool sensorsData;

        private static readonly Dictionary<string, DateTime> validities = new();

        private Alert(string message)
        {
            this.message = message;
            this.details = "";
            this.level = AlertLevel.Medium;
            this.validity = TimeSpan.Zero;
            this.filenames = new List<string>();
            this.camerasImages = true;
            this.sensorsData = true;
        }

        public static Alert Create(string message)
        {
            return new Alert(message);
        }

        public Alert Details(string details) // not part of validity check
        {
            this.details = details;
            return this;
        }

        public Alert Level(AlertLevel level)
        {
            this.level = level;
            return this;
        }

        public Alert Validity(TimeSpan validity)
        {
            this.validity = validity;
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
                logger.Info($"Send alert: {msg} ({this.level}, {this.validity}, {this.filenames.Count} files)");

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
                var emailPassword = Encoding.UTF8.GetString(Convert.FromBase64String(MyHome.Instance.Config.EmailPassword));
                if (!Services.SendEMail(MyHome.Instance.Config.SmtpServerAddress, MyHome.Instance.Config.Email, emailPassword,
                    MyHome.Instance.Config.Email, "My Home", emailMsg, this.filenames))
                {
                    result = false;
                }

                foreach (var file in images.Where(file => File.Exists(file)))
                    File.Delete(file);

                var smsPassword = Encoding.UTF8.GetString(Convert.FromBase64String(MyHome.Instance.Config.MyTelenorPassword));
                if (this.ShouldSendSMS() && !Services.SendSMS(MyHome.Instance.Config.GsmNumber, "telenor", smsPassword, msg))
                {
                    result = false;
                    Services.SendEMail(MyHome.Instance.Config.SmtpServerAddress, MyHome.Instance.Config.Email, emailPassword,
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
                if (this.validity != TimeSpan.Zero)
                    validities[this.message] = DateTime.Now + this.validity;
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
                if (camera.SaveImage(filename))
                    images.Add(filename);
            }
            return images;
        }

        private bool ShouldSendSMS()
        {
            if (this.level == AlertLevel.Critical)
                return true;

            if (this.level == AlertLevel.Low)
            {
                logger.Debug("Skip sending SMS since alert level is low");
                return false;
            }

            var quietHours = MyHome.Instance.Config.QuietHours;

            var currHour = DateTime.Now.Hour;
            if ((quietHours.start > quietHours.end && (currHour > quietHours.start || currHour < quietHours.end)) ||
                (quietHours.start < quietHours.end && (currHour > quietHours.start && currHour < quietHours.end)))
            {
                logger.Debug($"Skip sending SMS since quiet hours: {quietHours.start} - {quietHours.end}");
                return false;
            }
            return true;
        }
    }
}