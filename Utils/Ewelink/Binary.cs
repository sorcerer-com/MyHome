namespace MyHome.Utils.Ewelink
{
    using Newtonsoft.Json;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class Binary
    {
        [JsonProperty("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonProperty("digest")]
        public string? Digest { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }
    }
}