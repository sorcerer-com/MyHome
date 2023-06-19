using System.Collections.Generic;
using System.Linq;

namespace MyHome.Models
{
    public static class Selectors
    {
        // value, display name
        public static IEnumerable<(string, string)> GetRooms()
        {
            return MyHome.Instance.Rooms.Select(r => (r.Name, r.Name));
        }

        public static IEnumerable<(string, string)> GetDevices()
        {
            return MyHome.Instance.DevicesSystem.Devices.Select(d => ($"{d.Room.Name}.{d.Name}", $"{d.Room.Name}.{d.Name}")).OrderBy(d => d);
        }

        public static IEnumerable<(string, string)> GetSensors()
        {
            return MyHome.Instance.DevicesSystem.Sensors.Select(s => ($"{s.Room.Name}.{s.Name}", $"{s.Room.Name}.{s.Name}")).OrderBy(s => s);
        }

        public static IEnumerable<(string, string)> GetSensorsSubnames()
        {
            return MyHome.Instance.DevicesSystem.Sensors.SelectMany(s => s.Values.Keys).Distinct().OrderBy(s => s).Select(s => (s, s));
        }

        public static IEnumerable<(string, string)> GetSongs()
        {
            return MyHome.Instance.MediaPlayerSystem.Songs.OrderByDescending(kvp => kvp.Value).Select(kvp => (kvp.Key, kvp.Key));
        }
    }
}
