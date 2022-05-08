using System.IO;

using MyHome.Utils;

namespace MyHome.Models
{
    public class Config
    {
        public static readonly string BinPath = "bin";
        public static readonly string DataFilePath = Path.Join(BinPath, "data.json");

        [UiProperty(true, "sha256 hash")]
        public string Password { get; set; } = "";

        [UiProperty(true, "start - end")]
        public string QuietHours { get; set; } = "";


        [UiProperty(true, "08xxxxxxxx")]
        public string GsmNumber { get; set; } = "";

        [UiProperty(true)]
        public string MyTelenorPassword { get; set; } = "";

        [UiProperty(true, "host[:port]")]
        public string SmtpServerAddress { get; set; } = "";

        [UiProperty(true)]
        public string Email { get; set; } = "";

        [UiProperty(true)]
        public string EmailPassword { get; set; } = "";


        [UiProperty(true, "host[:port]")]
        public string MqttServerAddress { get; set; } = "";

        [UiProperty(true)]
        public string MqttUsername { get; set; } = "";

        [UiProperty(true)]
        public string MqttPassword { get; set; } = "";

        [UiProperty(true)]
        public string EwelinkEmail { get; set; }

        [UiProperty(true)]
        public string EwelinkPassword { get; set; }
    }
}
