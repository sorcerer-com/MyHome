using System.IO;

namespace MyHome.Models
{
    public class Config
    {
        public static readonly string BinPath = "bin";
        public static readonly string DataFilePath = Path.Join(BinPath, "data.json");

        public string QuietHours { get; set; } = "";

        public string GsmNumber { get; set; } = "";
        public string MyTelenorPassword { get; set; } = "";
        public string SmtpServerAddress { get; set; } = "";
        public string Email { get; set; } = "";
        public string EmailPassword { get; set; } = "";
    }
}
