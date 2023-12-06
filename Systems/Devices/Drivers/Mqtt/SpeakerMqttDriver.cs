using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;

using Newtonsoft.Json;

using static MyHome.Models.SongsManager;
using static MyHome.Systems.Devices.Drivers.Types.ISpeakerDriver;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class SpeakerMqttDriver : MqttDriver, ISpeakerDriver
    {
        private static readonly string Host = "http://192.168.0.100:5000";

        private static readonly Random random = new();

        private const string PLAYING_STATE_NAME = "Playing";
        private const string VOLUME_STATE_NAME = "Volume";
        private const string PAUSED_STATE_NAME = "Paused";
        private const string POSITION_STATE_NAME = "Position";
        private const string BUFFER_LEVEL_STATE_NAME = "BufferLevel";


        [JsonIgnore]
        [UiProperty]
        public string Playing => (string)this.States[PLAYING_STATE_NAME];

        [UiProperty]
        public int Volume
        {
            get => (int)this.States[VOLUME_STATE_NAME];
            set => this.SetStateAndSend(VOLUME_STATE_NAME, value);
        }

        [JsonIgnore]
        [UiProperty]
        public bool Paused
        {
            get => (bool)this.States[PAUSED_STATE_NAME];
            set => this.SetStateAndSend(PAUSED_STATE_NAME, value);
        }


        [JsonIgnore]
        [UiProperty]
        public int Position => (int)this.States[POSITION_STATE_NAME];

        [JsonIgnore]
        [UiProperty]
        public int BufferLevel => (int)this.States[BUFFER_LEVEL_STATE_NAME];


        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) PlayingGetMqttTopic
        {
            get => this.MqttGetTopics[PLAYING_STATE_NAME];
            set => this.SetGetTopic(PLAYING_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) PlayingSetMqttTopic
        {
            get => this.MqttSetTopics[PLAYING_STATE_NAME];
            set => this.MqttSetTopics[PLAYING_STATE_NAME] = value;
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) VolumeGetMqttTopic
        {
            get => this.MqttGetTopics[VOLUME_STATE_NAME];
            set => this.SetGetTopic(VOLUME_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) VolumeSetMqttTopic
        {
            get => this.MqttSetTopics[VOLUME_STATE_NAME];
            set => this.MqttSetTopics[VOLUME_STATE_NAME] = value;
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) PausedGetMqttTopic
        {
            get => this.MqttGetTopics[PAUSED_STATE_NAME];
            set => this.MqttGetTopics[PAUSED_STATE_NAME] = value;
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) PausedSetMqttTopic
        {
            get => this.MqttSetTopics[PAUSED_STATE_NAME];
            set => this.MqttSetTopics[PAUSED_STATE_NAME] = value;
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) PositionGetMqttTopic
        {
            get => this.MqttGetTopics[POSITION_STATE_NAME];
            set => this.MqttGetTopics[POSITION_STATE_NAME] = value;
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) BufferLevelGetMqttTopic
        {
            get => this.MqttGetTopics[BUFFER_LEVEL_STATE_NAME];
            set => this.MqttGetTopics[BUFFER_LEVEL_STATE_NAME] = value;
        }


        [JsonIgnore]
        [UiProperty]
        public List<SongInfo> Songs => MyHome.Instance.SongsManager.Songs;

        [JsonIgnore]
        [UiProperty]
        public List<int> Queue { get; }

        [UiProperty]
        public bool Shuffle { get; set; }

        [UiProperty(true)]
        public int AlarmVolume { get; set; }

        [UiProperty(true, "minutes")]
        public int AlarmDuration { get; set; }


        private readonly Action<Action> newStateReceivedDebouncer;
        private List<string> orderedSongs;
        private AlarmType? alarmType;


        public SpeakerMqttDriver()
        {
            this.States.Add(PLAYING_STATE_NAME, null);
            this.States.Add(VOLUME_STATE_NAME, 10);
            this.States.Add(PAUSED_STATE_NAME, false);
            this.States.Add(POSITION_STATE_NAME, 0);
            this.States.Add(BUFFER_LEVEL_STATE_NAME, 0);

            this.MqttGetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(PAUSED_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(POSITION_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(BUFFER_LEVEL_STATE_NAME, ("", ""));

            this.MqttSetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(PAUSED_STATE_NAME, ("", ""));

            this.Shuffle = false;
            this.AlarmVolume = 100;
            this.AlarmDuration = 5;

            this.Queue = new List<int>();

            this.newStateReceivedDebouncer = Utils.Utils.Debouncer(1000);
            this.orderedSongs = null;
            this.alarmType = null;
        }


        public void AddSong(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            MyHome.Instance.SongsManager.AddSong(value);
            this.orderedSongs = null;
            MyHome.Instance.SystemChanged = true;
        }

        public void RenameSong(string oldName, string newName)
        {
            if (MyHome.Instance.SongsManager.RenameSong(oldName, newName))
                MyHome.Instance.SystemChanged = true;
        }

        public void DeleteSong(string name, bool keepEntry = false)
        {
            if (MyHome.Instance.SongsManager.DeleteSong(name, keepEntry))
                MyHome.Instance.SystemChanged = true;
        }


        public void PlaySong(string name)
        {
            name = MyHome.Instance.SongsManager.EnsureSong(name); // ensure song is downloaded
            if (this.SetState(PLAYING_STATE_NAME, name))
            {
                if (!string.IsNullOrEmpty(name)) // set volume on start playing
                    this.SendState(VOLUME_STATE_NAME, this.Volume.ToString());
                else // on empty value - stop
                {
                    this.Queue.Clear();
                    this.orderedSongs = null;
                    this.alarmType = null;
                }

                var song = this.Songs.Find(s => s.Name == name);
                if (song?.Local == true || this.alarmType != null)
                    this.SendState(PLAYING_STATE_NAME, $"{Host}/api/songs/{Uri.EscapeDataString(name)}");
                else if (song?.Local == false)
                    this.SendState(PLAYING_STATE_NAME, song.Url);
                else
                    this.SendState(PLAYING_STATE_NAME, name);
            }
        }

        public void NextSong(string currentSong)
        {
            if (this.Queue.Count != 0)
            {
                var song = this.Songs[this.Queue[0]];
                this.Queue.RemoveAt(0);
                this.PlaySong(song.Name);
                this.orderedSongs = null; // regenerate order when queue get empty
                return;
            }

            if (this.orderedSongs == null)
            {
                if (this.Shuffle)
                {
                    var max = MyHome.Instance.SongsManager.Songs.Max(s => s.Rating);
                    this.orderedSongs = MyHome.Instance.SongsManager.Songs.Where(s => s.Exists)
                        .OrderByDescending(s => random.NextDouble() + ((double)s.Rating / max)).Select(s => s.Name).ToList();
                }
                else
                {
                    this.orderedSongs = MyHome.Instance.SongsManager.Songs.Where(s => s.Exists)
                        .OrderByDescending(s => s.Rating).Select(s => s.Name).ToList();
                }
            }
            this.PlaySong(this.orderedSongs[(this.orderedSongs.IndexOf(currentSong) + 1) % this.orderedSongs.Count]);
        }


        public void PlayAlarm(AlarmType alarmType)
        {
            if (this.alarmType == null)
            {
                this.alarmType = alarmType;
                this.PlaySong($"{alarmType}Alarm.mp3");
                this.Volume = this.AlarmVolume;

                // stop alarm after AlarmDuration minutes
                Task.Delay(TimeSpan.FromMinutes(this.AlarmDuration)).ContinueWith(_ =>
                {
                    if (this.alarmType != null)
                    {
                        this.PlaySong("");
                        this.Volume = 10;
                    }
                });
            }
        }


        protected override bool NewStateReceived(string name, object oldValue, object newValue)
        {
            base.NewStateReceived(name, oldValue, newValue);
            if (name == PLAYING_STATE_NAME)
            {
                // debounce because before starting a new song speaker stop the previous one
                this.newStateReceivedDebouncer(() =>
                {
                    // if we receive empty value for playing state and we want to loop songs, start a new one
                    if (string.IsNullOrEmpty((string)newValue) && !string.IsNullOrEmpty((string)oldValue))
                    {
                        if (this.alarmType != null) // if alarm was playing repeat it
                            this.PlaySong((string)oldValue);
                        else
                        {
                            MyHome.Instance.SongsManager.IncreaseSongRating((string)oldValue);
                            this.NextSong((string)oldValue);
                        }
                    }
                });
                this.States[name] = Uri.UnescapeDataString(((string)newValue).Replace($"{Host}/api/songs/", ""));
            }
            else if (name is POSITION_STATE_NAME or BUFFER_LEVEL_STATE_NAME) // do not save state on position or buffer level update
                return false;
            return true;
        }
    }
}
