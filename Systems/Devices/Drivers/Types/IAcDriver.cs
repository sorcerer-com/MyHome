namespace MyHome.Systems.Devices.Drivers.Types;

public interface IAcDriver
{
    public enum AcMode
    {
        Off,
        Auto,
        Cool,
        Heat,
        Dry,
        Fan
    }

    public enum AcFanSpeed
    {
        Auto,
        Min,
        Low,
        Medium,
        High,
        Max,
    }

    public enum AcSwingV
    {
        Auto,
        Off,
        Min,
        Low,
        Middle,
        High,
        Highest
    }

    public enum AcSwingH
    {
        Auto,
        Off,
        LeftMax,
        Left,
        Middle,
        Right,
        RightMax,
        Wide
    }

    bool Power { get; set; }
    AcMode Mode { get; set; }
    AcFanSpeed FanSpeed { get; set; }
    AcSwingV SwingV { get; set; }
    AcSwingH SwingH { get; set; }
    double Temperature { get; set; }
    bool Quiet { get; set; }
    bool Turbo { get; set; }
    bool Econo { get; set; }
    bool Light { get; set; }
    bool Filter { get; set; }
    bool Clean { get; set; }
    bool Beep { get; set; }
    int Sleep { get; set; }
}