using System.Collections.Generic;

namespace MyHome.Systems.Devices.Drivers.Types
{
    public interface IMediaDriver
    {
        long Length { get; }
        Dictionary<string, List<string>> MediaList { get; }
        bool Paused { get; }
        string Playing { get; }
        bool SortByDate { get; set; }
        long Time { get; set; }
        int Volume { get; set; }
        Dictionary<string, string> Watched { get; }

        void Pause();
        void Play(string path);
        void RefreshMediaList();
        void SeekBack();
        void SeekBackFast();
        void SeekForward();
        void SeekForwardFast();
        void StopMedia();
        void VolumeDown();
        void VolumeUp();
    }
}