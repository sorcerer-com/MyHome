using System;
using System.Collections.Generic;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;

namespace MyHome.Systems.Devices.Drivers.Mqtt
{
    public class MediaAgentMqttDriver : MqttDriver
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private const string MEDIA_LIST_STATE_NAME = "MediaList";
        private const string PLAYING_STATE_NAME = "Playing";
        private const string STATE_STATE_NAME = "State";
        private const string VOLUME_STATE_NAME = "Volume";
        private const string TIME_STATE_NAME = "Time";
        private const string LENGTH_STATE_NAME = "Length";


        [JsonIgnore]
        [UiProperty]
        public Dictionary<string, List<string>> MediaList => this.GetMediaList();

        [JsonIgnore]
        [UiProperty]
        public string Playing => ((string)this.States[PLAYING_STATE_NAME]).Replace("file:///", "");

        [UiProperty]
        public int Volume
        {
            get => (int)this.States[VOLUME_STATE_NAME];
            set => this.SetStateAndSend(VOLUME_STATE_NAME, value);
        }

        [JsonIgnore]
        [UiProperty]
        public bool Paused => (string)this.States[STATE_STATE_NAME] == "State.Paused";

        [JsonIgnore]
        [UiProperty]
        public long Time
        {
            get => (long)this.States[TIME_STATE_NAME];
            set => this.SetStateAndSend(TIME_STATE_NAME, value);
        }

        [JsonIgnore]
        [UiProperty]
        public long Length => (long)this.States[LENGTH_STATE_NAME];


        [UiProperty]
        public bool SortByDate { get; set; }

        [UiProperty]
        public List<string> Watched { get; }


        private string agentHostName;
        [UiProperty(true, "e.g. raspberrypi")]
        public string AgentHostName
        {
            get => this.agentHostName;
            set
            {
                if (this.agentHostName == value) return;
                this.agentHostName = value;

                this.MqttGetTopics[MEDIA_LIST_STATE_NAME] = ($"tele/{this.agentHostName}/MEDIA_LIST", "");
                this.MqttGetTopics[PLAYING_STATE_NAME]    = ($"tele/{this.agentHostName}/MEDIA", "playing");
                this.MqttGetTopics[STATE_STATE_NAME]      = ($"tele/{this.agentHostName}/MEDIA", "state");
                this.MqttGetTopics[VOLUME_STATE_NAME]     = ($"tele/{this.agentHostName}/MEDIA", "volume");
                this.MqttGetTopics[TIME_STATE_NAME]       = ($"tele/{this.agentHostName}/MEDIA", "time");
                this.MqttGetTopics[LENGTH_STATE_NAME]     = ($"tele/{this.agentHostName}/MEDIA", "length");

                this.MqttSetTopics[PLAYING_STATE_NAME]    = ($"cmnd/{this.agentHostName}/media", "play");
                this.MqttSetTopics[VOLUME_STATE_NAME]     = ($"cmnd/{this.agentHostName}/media", "volume");
                this.MqttSetTopics[TIME_STATE_NAME]       = ($"cmnd/{this.agentHostName}/media", "time");
            }
        }


        public MediaAgentMqttDriver()
        {
            this.States.Add(MEDIA_LIST_STATE_NAME, "{}");
            this.States.Add(PLAYING_STATE_NAME, "");
            this.States.Add(STATE_STATE_NAME, null);
            this.States.Add(VOLUME_STATE_NAME, 50);
            this.States.Add(TIME_STATE_NAME, -1L);
            this.States.Add(LENGTH_STATE_NAME, -1L);

            this.MqttGetTopics.Add(MEDIA_LIST_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(STATE_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(TIME_STATE_NAME, ("", ""));
            this.MqttGetTopics.Add(LENGTH_STATE_NAME, ("", ""));

            this.MqttSetTopics.Add(PLAYING_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(TIME_STATE_NAME, ("", ""));

            this.Watched = new List<string>();
        }

        public override void Stop()
        {
            base.Stop();
            this.StopMedia();
        }


        public void Play(string path)
        {
            logger.Debug($"Play media: {path}");
            if (string.IsNullOrEmpty(path))
                return;

            this.SendState(PLAYING_STATE_NAME, path);
            this.SendState(VOLUME_STATE_NAME, this.Volume.ToString());
            this.MarkWatched(path);
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaPlayed, path);
        }

        public void StopMedia()
        {
            logger.Debug($"Stop media: {this.Playing}");
            if (MyHome.Instance.MqttClient.IsConnected)
                MyHome.Instance.MqttClient.Publish($"cmnd/{this.AgentHostName}/media", "{\"stop\": true}");
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaStopped);
        }

        public void Pause()
        {
            logger.Debug($"Pause media: {this.Playing}");
            if (MyHome.Instance.MqttClient.IsConnected)
                MyHome.Instance.MqttClient.Publish($"cmnd/{this.AgentHostName}/media", $"{{\"pause\": {(this.Paused ? "false" : "true")}}}");
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaPaused);
        }

        public void VolumeDown()
        {
            logger.Debug($"Volume down media: {this.Playing} to {this.Volume - 5}");
            this.Volume -= 5;
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaVolumeDown);
        }

        public void VolumeUp()
        {
            logger.Debug($"Volume up media: {this.Playing} to {this.Volume + 5}");
            this.Volume += 5;
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaVolumeUp);
        }

        public void SeekBack()
        {
            logger.Debug($"Seek back media: {this.Playing}");
            this.Time -= 30 * 1000; // -30 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekBack);
        }

        public void SeekForward()
        {
            logger.Debug($"Seek forward media: {this.Playing}");
            this.Time += 30 * 1000; // +30 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekForward);
        }

        public void SeekBackFast()
        {
            logger.Debug($"Seek back fast media: {this.Playing}");
            this.Time -= 600 * 1000; // -600 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekBackFast);
        }

        public void SeekForwardFast()
        {
            logger.Debug($"Seek forward fast media: {this.Playing}");
            this.Time += 600 * 1000; // +600 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekForwardFast);
        }

        public void RefreshMediaList()
        {
            logger.Debug("Refresh media list");
            if (MyHome.Instance.MqttClient.IsConnected)
                MyHome.Instance.MqttClient.Publish($"cmnd/{this.AgentHostName}/media", "{\"refresh\": true}");
        }

        public void PowerOffTV()
        {
            logger.Debug("Power off TV");
            if (MyHome.Instance.MqttClient.IsConnected)
            {
                var json = new JObject
                {
                    ["address"] = "TV_0",
                    ["command"] = "standby"
                };
                MyHome.Instance.MqttClient.Publish($"cmnd/{this.AgentHostName}/cec", json.ToString());
            }
        }


        protected override bool NewStateReceived(string name, object oldValue, object newValue)
        {
            base.NewStateReceived(name, oldValue, newValue);
            if (name == VOLUME_STATE_NAME && (int)newValue == -1) // on stop volume is set to -1
                this.States[VOLUME_STATE_NAME] = oldValue;
            return false; // don't save
        }

        private Dictionary<string, List<string>> GetMediaList()
        {
            var result = new Dictionary<string, List<string>>();

            var list = JObject.Parse((string)this.States[MEDIA_LIST_STATE_NAME]);
            foreach (var p in list.Properties())
            {
                var paths = p.Value.OrderBy(t => this.SortByDate ? t[1] : t[0]) // t[0] - path, t[1] - last modified date
                    .Select(t => (string)t[0]).ToList();
                result.Add(p.Name, paths);
            }
            return result;
        }

        private void MarkWatched(string path)
        {
            logger.Debug($"Mark as watched: {path}");

            if (!this.Watched.Contains(path))
                this.Watched.Add(path);

            // cleanup watched list from nonexistent files
            var list = this.MediaList.SelectMany(kvp => kvp.Value.Select(v => System.IO.Path.Join(kvp.Key, v)));
            this.Watched.RemoveAll(w => !list.Contains(w));
            MyHome.Instance.SystemChanged = true;
        }
    }
}
