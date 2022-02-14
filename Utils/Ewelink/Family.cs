namespace MyHome.Utils.Ewelink
{
    using Newtonsoft.Json;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class Family
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("index")]
        public int? Index { get; set; }
    }
}