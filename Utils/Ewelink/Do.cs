using Newtonsoft.Json;

namespace MyHome.Utils.Ewelink
{
    public class Do
    {
        [JsonProperty("switch")]
        public SwitchState? Switch { get; set; }
    }
}