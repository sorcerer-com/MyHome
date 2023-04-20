using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class SpeakerMqttDriver : MqttDriver
    {
        private static readonly string Host = "http://192.168.0.100:5000";

        private static readonly Random random = new();

        public enum AlarmType
        {
            Fire,
            Security
        }

        private const string PLAYING_STATE_NAME = "Playing";
        private const string VOLUME_STATE_NAME = "Volume";
        private const string PAUSED_STATE_NAME = "Paused";
        private const string POSITION_STATE_NAME = "Position";
        private const string BUFFER_LEVEL_STATE_NAME = "BufferLevel";

        [UiProperty(selector: "GetSongs")]
        public string Playing
        {
            get => (string)this.States[PLAYING_STATE_NAME];
            set
            {
                value = MyHome.Instance.MediaPlayerSystem.EnsureSong(value); // ensure song is downloaded
                if (this.SetState(PLAYING_STATE_NAME, value))
                {
                    if (!string.IsNullOrEmpty(value)) // set volume on start playing
                        this.SendState(VOLUME_STATE_NAME, this.Volume.ToString());
                    else // on empty value - stop
                    {
                        this.orderedSongs = null;
                        this.alarmType = null;
                    }
                    this.SendPlayingState();
                }
            }
        }

        [JsonIgnore]
        [UiProperty]
        public string PlayYouTube
        {
            get => "";
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var path = MyHome.Instance.MediaPlayerSystem.AddSong(value);
                    this.orderedSongs = null;
                    this.Playing = path;
                }
            }
        }

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

        [UiProperty]
        public bool Loop { get; set; }

        [UiProperty]
        public bool Shuffle { get; set; }

        [UiProperty(true)]
        public int AlarmVolume { get; set; }

        [UiProperty(true, "minutes")]
        public int AlarmDuration { get; set; }


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

            this.Loop = false;
            this.Shuffle = false;
            this.AlarmVolume = 100;
            this.AlarmDuration = 5;

            this.newStateReceivedDebouncer = Utils.Utils.Debouncer(1000);
            this.orderedSongs = null;
            this.alarmType = null;
        }

        public void NextSong(string currentSong)
        {
            if (this.orderedSongs == null)
            {
                if (this.Shuffle)
                {
                    var max = MyHome.Instance.MediaPlayerSystem.Songs.Values.Max();
                    this.orderedSongs = MyHome.Instance.MediaPlayerSystem.Songs
                        .OrderByDescending(kvp => random.NextDouble() + (double)kvp.Value / max).Select(kvp => kvp.Key)
                        .Where(s => File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, s))).ToList();
                }
                else
                    this.orderedSongs = MyHome.Instance.MediaPlayerSystem.Songs.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key)
                        .Where(s => File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, s))).ToList();
            }
            this.Playing = this.orderedSongs[(this.orderedSongs.IndexOf(currentSong) + 1) % this.orderedSongs.Count];
        }

        public void PlayAlarm(AlarmType alarmType)
        {
            if (this.alarmType == null)
            {
                this.alarmType = alarmType;
                this.Playing = $"{alarmType}Alarm.mp3";
                this.Volume = this.AlarmVolume;

                // stop alarm after AlarmDuration minutes
                Task.Delay(TimeSpan.FromMinutes(this.AlarmDuration)).ContinueWith(_ =>
                {
                    if (this.alarmType != null)
                    {
                        this.Playing = "";
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
                            this.Playing = (string)oldValue;
                        else
                        {
                            MyHome.Instance.MediaPlayerSystem.IncreaseSongRating((string)oldValue);
                            if (this.Loop)
                                this.NextSong((string)oldValue);
                        }
                    }
                });
                this.States[name] = Uri.UnescapeDataString(((string)newValue).Replace($"{Host}/api/systems/MediaPlayer/songs/", ""));
            }
            else if (name == POSITION_STATE_NAME || name == BUFFER_LEVEL_STATE_NAME) // do not save state on position or buffer level update
                return false;
            return true;
        }

        private void SendPlayingState()
        {
            var value = Uri.EscapeDataString(this.Playing);
            if (!string.IsNullOrEmpty(value) && !value.StartsWith("http"))
                value = $"{Host}/api/systems/MediaPlayer/songs/{value}";

            this.SendState(PLAYING_STATE_NAME, value);
        }
    }
}
