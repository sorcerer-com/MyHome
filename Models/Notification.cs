using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Models
{
    public class Notification
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public enum NotificationLevel
        {
            Low,
            Medium,
            Critical
        }

        private readonly string message;
        private DateTime time;
        private string details;
        private NotificationLevel level;
        private (TimeSpan init, TimeSpan snooze) validity;

        private bool sent = false;

        public Notification(string message)
        {
            this.message = message;
            this.time = DateTime.Now;
            this.details = "";
            this.level = NotificationLevel.Medium;
            this.validity = (TimeSpan.Zero, TimeSpan.Zero);
        }

        public object ToUiObject()
        {
            return new
            {
                Time = this.time,
                Message = this.message,
                Details = this.details,
                Level = this.level,
                Validity = this.Validity(),
                IsValid = this.Validity() == TimeSpan.Zero || DateTime.Now < this.time + this.Validity()
            };
        }

        public Notification Details(string details) // not part of validity check
        {
            this.details = details;
            return this;
        }

        public Notification Level(NotificationLevel level)
        {
            this.level = level;
            return this;
        }

        public Notification Validity(TimeSpan validity)
        {
            this.time = DateTime.Now;
            this.validity.init = validity;
            return this;
        }

        public string Message() => this.message;

        public string Details() => this.details;

        public NotificationLevel Level() => this.level;

        public TimeSpan Validity() => this.validity.init + this.validity.snooze;


        public TimeSpan Snooze(TimeSpan time)
        {
            this.validity.snooze = time;
            return this.Validity();
        }

        public bool SendAlert(List<string> filenames = null, bool camerasImages = true, bool sensorsData = true, bool forceSend = false)
        {
            if (MyHome.Instance.BackupMode && !forceSend)
            {
                logger.Debug("Skip sending alert as working in backup mode");
                return true;
            }

            try
            {
                var msg = $"{this.message} {this.details}";
                if (this.Validity() != TimeSpan.Zero && DateTime.Now < this.time + this.Validity() && this.sent)
                {
                    logger.Trace($"Don't send new alert since there is valid alert until: {this.time + this.Validity()}");
                    return true;
                }
                this.sent = true;

                filenames ??= new List<string>();
                var images = new List<string>();
                if (camerasImages)
                    images.AddRange(GetCameraImages());

                logger.Info($"Send alert: {msg} ({this.level}, {this.Validity()}, {filenames.Count + images.Count} files)");

                bool result = true;
                msg = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss}\n{msg}";

                var emailMsg = msg;
                if (sensorsData)
                {
                    var latestData = MyHome.Instance.DevicesSystem.Sensors.ToDictionary(s => s.Room.Name + "." + s.Name, s => s.Values);
                    emailMsg = $"{msg}\n\n{JsonConvert.SerializeObject(latestData, Formatting.Indented)}";
                }
                var emailPassword = Encoding.UTF8.GetString(Convert.FromBase64String(MyHome.Instance.Config.EmailPassword));
                if (!Services.SendEMail(MyHome.Instance.Config.SmtpServerAddress, MyHome.Instance.Config.Email, emailPassword,
                    MyHome.Instance.Config.Email, "My Home", emailMsg, filenames.Union(images).ToList()))
                {
                    // retry after 10 minutes
                    Task.Delay(TimeSpan.FromMinutes(10)).ContinueWith(_ => this.SendAlert(filenames, camerasImages, sensorsData, forceSend));

                    result = false;
                }

                foreach (var file in images.Where(File.Exists)) // delete camera images
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


        private bool ShouldSendSMS()
        {
            if (string.IsNullOrEmpty(MyHome.Instance.Config.GsmNumber)) // don't try to send sms if gsm number is not set
                return false;

            if (this.level == NotificationLevel.Critical)
                return true;

            if (this.level == NotificationLevel.Low)
            {
                logger.Debug("Skip sending SMS since notification level is low");
                return false;
            }

            var quietHours = MyHome.Instance.Config.QuietHours;

            var currHour = DateTime.Now.Hour;
            if ((quietHours.start > quietHours.end && (currHour > quietHours.start || currHour < quietHours.end)) ||
                (quietHours.start < quietHours.end && currHour > quietHours.start && currHour < quietHours.end))
            {
                logger.Debug($"Skip sending SMS since quiet hours: {quietHours.start} - {quietHours.end}");
                return false;
            }

            return true;
        }

        private static List<string> GetCameraImages()
        {
            var images = new List<string>();
            foreach (var camera in MyHome.Instance.DevicesSystem.Cameras)
            {
                var filename = Path.Combine(Config.BinPath, $"{camera.Room.Name}_{camera.Name}.jpg");
                if (camera.SaveImage(filename))
                    images.Add(filename);
            }
            return images;
        }
    }
}