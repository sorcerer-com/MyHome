using System;
using System.Collections.Generic;
using System.Linq;

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

        [JsonIgnore]
        [UiProperty(selector: "GetSongs")]
        public string Playing
        {
            get => (string)this.States[PLAYING_STATE_NAME];
            set
            {
                if (this.SetState(PLAYING_STATE_NAME, value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (MyHome.Instance.MediaPlayerSystem.Songs.ContainsKey(value))
                            MyHome.Instance.MediaPlayerSystem.Songs[value] = MyHome.Instance.MediaPlayerSystem.Songs[value] + 1;
                        this.SendState(VOLUME_STATE_NAME, this.Volume.ToString());
                    }
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

        [UiProperty]
        public bool Paused
        {
            get => (bool)this.States[PAUSED_STATE_NAME];
            set => this.SetStateAndSend(PAUSED_STATE_NAME, value);
        }

        [UiProperty]
        public bool Loop { get; set; }

        [UiProperty]
        public bool Shuffle { get; set; }

        [UiProperty]
        public int AlarmVolume { get; set; }


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


        private readonly Action<Action> newStateReceivedDebouncer;
        private List<string> orderedSongs;
        private AlarmType? alarmType;


        public SpeakerMqttDriver()
        {
            this.States.Add(PLAYING_STATE_NAME, null);
            this.States.Add(VOLUME_STATE_NAME, 10);
            this.States.Add(PAUSED_STATE_NAME, false);

            this.MqttGetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(PAUSED_STATE_NAME, ("", ""));

            this.MqttSetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(PAUSED_STATE_NAME, ("", ""));

            this.Loop = false;
            this.Shuffle = false;
            this.AlarmVolume = 100;

            this.newStateReceivedDebouncer = Utils.Utils.Debouncer(1000);
            this.orderedSongs = null;
            this.alarmType = null;
        }

        public void NextSong(string currentSong)
        {
            if (this.orderedSongs == null)
            {
                if (this.Shuffle)
                    this.orderedSongs = MyHome.Instance.MediaPlayerSystem.Songs.Keys.OrderBy(_ => random.Next()).ToList();
                else
                    this.orderedSongs = MyHome.Instance.MediaPlayerSystem.Songs.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            }
            this.Playing = this.orderedSongs[(this.orderedSongs.IndexOf(currentSong) + 1) % this.orderedSongs.Count];
        }

        public void PlayAlarm(AlarmType alarmType)
        {
            this.alarmType = alarmType;
            this.Playing = $"{alarmType}Alarm.mp3";
            this.Volume = this.AlarmVolume;
        }


        protected override void NewStateReceived(string name, object oldValue, object newValue)
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
                        else if (this.Loop)
                            this.NextSong((string)oldValue);
                    }
                });
                this.States[name] = Uri.UnescapeDataString(((string)newValue).Replace($"{Host}/api/systems/MediaPlayer/songs/", ""));
            }
        }


        private void SendPlayingState()
        {
            var value = Uri.EscapeUriString(this.Playing);
            if (!string.IsNullOrEmpty(value) && !value.StartsWith("http"))
                value = $"{Host}/api/systems/MediaPlayer/songs/{value}";

            this.SendState(PLAYING_STATE_NAME, value);
        }
    }
}
