using Newtonsoft.Json;

namespace MyHome.Utils.Ewelink
{
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class Credentials
    {
        [JsonProperty("at")]
        public string? At { get; set; }

        [JsonProperty("rt")]
        public string? Rt { get; set; }

        [JsonProperty("user")]
        public User? User { get; set; }

        [JsonProperty("region")]
        public string? Region { get; set; }
    }
}