namespace MyHome.Systems.Devices.Drivers.Types;

public interface ISpeakerDriver
{
    public enum AlarmType
    {
        Fire,
        Security
    }

    string Playing { get; set; }
    string PlayYouTube { get; set; }
    int Volume { get; set; }
    bool Paused { get; set; }
    int Position { get; }
    int BufferLevel { get; }
    bool Loop { get; set; }
    bool Shuffle { get; set; }
    int AlarmVolume { get; set; }
    int AlarmDuration { get; set; }

    void NextSong(string currentSong);
    void PlayAlarm(AlarmType alarmType);
}