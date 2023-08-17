using System;

namespace MyHome.Models;

public class Notification
{
    public const string UpgradeType = "upgrade";
    public const string DiscoveredDeviceType = "discovered_device";
    public const string OfflineDeviceType = "offline_device";


    public DateTime Time { get; set; }

    public string Type { get; set; }

    public string Message { get; set; }

    public bool Expired => this.Duration != TimeSpan.Zero && DateTime.Now > this.Time + this.Duration;


    private readonly TimeSpan Duration;


    public Notification(string type, string message, TimeSpan? duration = null)
    {
        this.Time = DateTime.Now;
        this.Type = type;
        this.Message = message;
        this.Duration = duration ?? TimeSpan.Zero;
    }
}