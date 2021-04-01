using System.IO;

using MyHome.Utils;

namespace MyHome.Models
{
    public class Config
    {
        public static readonly string BinPath = "bin";
        public static readonly string DataFilePath = Path.Join(BinPath, "data.json");

        [UiProperty(true)]
        public string Password { get; set; } = "";

        [UiProperty(true)]
        public string QuietHours { get; set; } = "";


        [UiProperty(true)]
        public string GsmNumber { get; set; } = "";

        [UiProperty(true)]
        public string MyTelenorPassword { get; set; } = "";

        [UiProperty(true)]
        public string SmtpServerAddress { get; set; } = "";

        [UiProperty(true)]
        public string Email { get; set; } = "";

        [UiProperty(true)]
        public string EmailPassword { get; set; } = "";
    }
}
