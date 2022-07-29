using System;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class SpeakerMqttDriver : MqttDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        private static readonly Random random = new Random();

        private const string PLAYING_STATE_NAME = "Playing";
        private const string VOLUME_STATE_NAME = "Volume";

        [JsonIgnore]
        [UiProperty(selector: "GetSongs")]
        public string Playing
        {
            get => (string)this.State[PLAYING_STATE_NAME];
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    MyHome.Instance.MediaPlayerSystem.Songs[value] = MyHome.Instance.MediaPlayerSystem.Songs[value] + 1;
                    this.SendVolumeState();
                }
                this.SetState(PLAYING_STATE_NAME, value, this.SendSpeakerState);
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
            get => (int)this.State[VOLUME_STATE_NAME];
            set => this.SetState(VOLUME_STATE_NAME, value, this.SendVolumeState);
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
        public (string topic, string jsonPath) PlayingSetMqttTopic { get; set; }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) VolumeGetMqttTopic
        {
            get => this.MqttGetTopics[VOLUME_STATE_NAME];
            set => this.SetGetTopic(VOLUME_STATE_NAME, value);
        }

        [UiProperty(true, "(topic, json path)")]
        public (string topic, string jsonPath) VolumeSetMqttTopic { get; set; }

        // TODO: state getted from MQTT is with the full url and don't match the selector in UI


        public SpeakerMqttDriver()
        {
            this.State.Add(PLAYING_STATE_NAME, null);
            this.State.Add(VOLUME_STATE_NAME, 10);
            this.MqttGetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.PlayingSetMqttTopic = ("", "");
            this.VolumeSetMqttTopic = ("", "");
        }

        protected override void NewStateReceived(string state, object oldValue, object newValue)
        {
            base.NewStateReceived(state, oldValue, newValue);
            // if we receive empty value for playing state and we want to loop songs, start a new one
            if (state == PLAYING_STATE_NAME && string.IsNullOrEmpty((string)newValue) && this.Loop)
            {
                var songs = MyHome.Instance.MediaPlayerSystem.Songs.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                if (this.Shuffle)
                {
                    songs.Remove((string)oldValue); // remove current song and pick from the others
                    this.Playing = songs[random.Next(songs.Count)];
                }
                else
                    this.Playing = songs[(songs.IndexOf((string)oldValue) + 1) % songs.Count];
            }
        }


        private void SendSpeakerState()
        {
            var value = Uri.EscapeUriString(this.Playing);
            if (!string.IsNullOrEmpty(value) && !value.StartsWith("http"))
                value = $"{Startup.Host}/api/systems/MediaPlayer/songs/{value}";

            if (!string.IsNullOrEmpty(this.PlayingSetMqttTopic.jsonPath))
            {
                var json = new JObject
                {
                    [this.PlayingSetMqttTopic.jsonPath] = value
                };
                value = json.ToString();
            }

            if (MyHome.Instance.MqttClient.IsConnected)
            {
                logger.Info($"Send state to {this.Name} ({this.Room.Name}): {value}");
                MyHome.Instance.MqttClient.Publish(this.PlayingSetMqttTopic.topic, value);
            }
        }

        private void SendVolumeState()
        {
            var value = this.Volume.ToString();
            if (!string.IsNullOrEmpty(this.VolumeSetMqttTopic.jsonPath))
            {
                var json = new JObject
                {
                    [this.VolumeSetMqttTopic.jsonPath] = value
                };
                value = json.ToString();
            }

            if (MyHome.Instance.MqttClient.IsConnected)
            {
                logger.Info($"Send state to {this.Name} ({this.Room.Name}): {value}");
                MyHome.Instance.MqttClient.Publish(this.VolumeSetMqttTopic.topic, value);
            }
        }
    }
}
