namespace MyHome.Utils.Ewelink
{
    using Newtonsoft.Json;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class AppInfo
    {
        [JsonProperty("os")]
        public string? Os { get; set; }

        [JsonProperty("appVersion")]
        public string? AppVersion { get; set; }
    }
}