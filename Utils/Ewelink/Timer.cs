namespace MyHome.Utils.Ewelink
{

    using Newtonsoft.Json;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class Timer
    {
        [JsonProperty("mId")]
        public string? MId { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("at")]
        public string? At { get; set; }

        [JsonProperty("coolkit_timer_type")]
        public string? CoolkitTimerType { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("do")]
        public Do? Do { get; set; }

        [JsonProperty("period")]
        public string? Period { get; set; }

    }
}