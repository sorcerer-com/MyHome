using System.Collections.Generic;

using Newtonsoft.Json;

namespace MyHome.Utils.Ewelink
{
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class BindInfos
    {
        [JsonProperty("gaction")]
        public List<string>? Gaction { get; set; }

        [JsonProperty("iftttTriggerCnt")]
        public int IftttTriggerCount { get; set; }
    }
}