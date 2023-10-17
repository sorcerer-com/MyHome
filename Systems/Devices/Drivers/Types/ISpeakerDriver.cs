namespace MyHome.Systems.Devices.Drivers.Types;

public interface ISpeakerDriver
{
    public enum AlarmType
    {
        Fire,
        Security
    }

    string Playing { get; }
    int Volume { get; set; }
    bool Paused { get; set; }
    int Position { get; }
    int BufferLevel { get; }
    bool Shuffle { get; set; }
    int AlarmVolume { get; set; }
    int AlarmDuration { get; set; }


    void PlaySong(string name);
    void NextSong(string currentSong);

    void PlayAlarm(AlarmType alarmType);
}