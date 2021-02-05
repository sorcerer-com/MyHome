using System.IO;

namespace MyHome
{
    public class Config
    {
        public static readonly string DataFilePath = Path.Join("bin", "data.json");

        public string QuietHours { get; set; } = "";

        public string GsmNumber { get; set; } = "";
        public string MyTelenorPassword { get; set; } = "";
        public string SmtpServerAddress { get; set; } = "";
        public string Email { get; set; } = "";
        public string EmailPassword { get; set; } = "";
    }
}
