namespace MyHome.Systems.Devices.Drivers.Types;

public interface ISwitchDriver
{
    bool IsOn { get; set; }
    bool ConfirmationRequired { get; set; }

    bool Toggle();
}