using System.Collections.Generic;

using Newtonsoft.Json;

namespace MyHome.Utils.Ewelink
{
    public class Tags
    {

        [JsonProperty("m_760c_arch")]
        public string M760cArch { get; set; }

        [JsonProperty("disable_timers")]
        public List<object> DisableTimers { get; set; }

        [JsonProperty("zyx_info")]
        public List<object> ZyxInfo { get; set; }

    }
}