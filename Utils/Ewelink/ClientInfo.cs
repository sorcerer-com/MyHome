namespace MyHome.Utils.Ewelink
{
    using Newtonsoft.Json;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class ClientInfo
    {
        [JsonProperty("model")]
        public string? Model { get; set; }

        [JsonProperty("os")]
        public string? Os { get; set; }

        [JsonProperty("imei")]
        public string? Imei { get; set; }

        [JsonProperty("romVersion")]
        public string? RomVersion { get; set; }

        [JsonProperty("appVersion")]
        public string? AppVersion { get; set; }
    }
}