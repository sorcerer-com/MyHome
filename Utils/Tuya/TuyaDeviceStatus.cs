using Newtonsoft.Json;

namespace MyHome.Utils.Tuya;

/// <summary>
/// Currect device status.
/// </summary>
public class TuyaDeviceStatus
{
    /// <summary>
    /// DPS number
    /// </summary>
    [JsonProperty("code")]
    public string Code { get; set; }

    /// <summary>
    /// DPS value.
    /// </summary>
    [JsonProperty("value")]
    public object Value { get; set; }
}