using System;

using Newtonsoft.Json;

namespace MyHome.Utils.Tuya;

/// <summary>
/// Device info received from local network.
/// </summary>
public sealed class TuyaDeviceScanInfo : IEquatable<TuyaDeviceScanInfo>
{
    [JsonProperty("ip")]
    public string IP { get; set; } = null;

    [JsonProperty("gwId")]
    public string GwId { get; set; } = null;

    [JsonProperty("active")]
    public int Active { get; set; } = 0;

    [JsonProperty("ability")]
    public int Ability { get; set; } = 0;

    [JsonProperty("mode")]
    public int Mode { get; set; } = 0;

    [JsonProperty("encrypt")]
    public bool Encryption { get; set; } = false;

    [JsonProperty("productKey")]
    public string ProductKey { get; set; } = null;

    [JsonProperty("version")]
    public string Version { get; set; } = null;

    public bool Equals(TuyaDeviceScanInfo other)
    {
        return (this.IP == other.IP) && (this.GwId == other.GwId);
    }

    public override bool Equals(object obj)
    {
        return this.Equals(obj as TuyaDeviceScanInfo);
    }

    public override string ToString()
    {
        return $"IP: {this.IP}, gwId: {this.GwId}, product key: {this.ProductKey}, encryption: {this.Encryption}, version: {this.Version}";
    }

    public override int GetHashCode()
    {
        return this.IP.GetHashCode() + this.GwId.GetHashCode();
    }
}
