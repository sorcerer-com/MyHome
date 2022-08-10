using System;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class SpeakerMqttDriver : MqttDriver
    {
        private static readonly string Host = "http://192.168.0.100:5000";

        private static readonly Random random = new();

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
                        MyHome.Instance.MediaPlayerSystem.Songs[value] = MyHome.Instance.MediaPlayerSystem.Songs[value] + 1;
                        this.SendState(VOLUME_STATE_NAME, this.Volume.ToString());
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
        }

        public void NextSong(string currentSong)
        {
            var songs = MyHome.Instance.MediaPlayerSystem.Songs.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            if (this.Shuffle)
            {
                songs.Remove(currentSong); // remove current song and pick from the others
                this.Playing = songs[random.Next(songs.Count)];
            }
            else
                this.Playing = songs[(songs.IndexOf(currentSong) + 1) % songs.Count];
        }


        protected override void NewStateReceived(string name, object oldValue, object newValue)
        {
            base.NewStateReceived(name, oldValue, newValue);
            if (name == PLAYING_STATE_NAME)
            {
                // if we receive empty value for playing state and we want to loop songs, start a new one
                if (string.IsNullOrEmpty((string)newValue) && this.Loop)
                    this.NextSong((string)oldValue);

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
