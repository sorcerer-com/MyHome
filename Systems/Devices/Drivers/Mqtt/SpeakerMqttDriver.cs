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
        private static readonly string Host = $"http://{Environment.MachineName}.local:5000";

        private static readonly Random random = new();

        private const string PLAYING_STATE_NAME = "Playing";
        private const string TITLE_STATE_NAME = "Title";
        private const string VOLUME_STATE_NAME = "Volume";
        private const string PAUSED_STATE_NAME = "Paused";
        private const string POSITION_STATE_NAME = "Position";
        private const string BUFFER_LEVEL_STATE_NAME = "BufferLevel";


        [JsonIgnore]
        [UiProperty]
        public string Playing => (string)this.States[PLAYING_STATE_NAME];

        [JsonIgnore]
        [UiProperty]
        public string Title => (string)this.States[TITLE_STATE_NAME];

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
        public (string topic, string jsonPath) TitleSetMqttTopic
        {
            get => this.MqttSetTopics[TITLE_STATE_NAME];
            set => this.MqttSetTopics[TITLE_STATE_NAME] = value;
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
        public List<Playlist> Playlists => MyHome.Instance.SongsManager.Playlists;

        [JsonIgnore]
        [UiProperty]
        public List<int> Queue { get; }

        [UiProperty]
        public bool Shuffle { get; set; }

        [JsonIgnore]
        [UiProperty]
        public string ActivePlaylist
        {
            get => this.activePlaylist;
            set
            {
                if (this.activePlaylist != value)
                {
                    this.activePlaylist = value;
                    this.orderedSongs = null; // regenerate order for the new playlist
                }
            }
        }

        [UiProperty(true)]
        public int AlarmVolume { get; set; }

        [UiProperty(true, "minutes")]
        public int AlarmDuration { get; set; }


        private List<string> orderedSongs;
        private string activePlaylist;
        private AlarmType? alarmType;


        public SpeakerMqttDriver()
        {
            this.States.Add(PLAYING_STATE_NAME, null);
            this.States.Add(TITLE_STATE_NAME, null);
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
            this.MqttSetTopics.Add(TITLE_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(VOLUME_STATE_NAME, ("", ""));
            this.MqttSetTopics.Add(PAUSED_STATE_NAME, ("", ""));

            this.Shuffle = false;
            this.AlarmVolume = 100;
            this.AlarmDuration = 5;

            this.Queue = new List<int>();

            this.orderedSongs = null;
            this.activePlaylist = "";
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


        public void CreatePlaylist(string name)
        {
            if (MyHome.Instance.SongsManager.CreatePlaylist(name))
                MyHome.Instance.SystemChanged = true;
        }

        public void DeletePlaylist(string name)
        {
            if (MyHome.Instance.SongsManager.DeletePlaylist(name))
            {
                if (this.ActivePlaylist == name)
                {
                    this.ActivePlaylist = "";
                    this.orderedSongs = null;
                }
                MyHome.Instance.SystemChanged = true;
            }
        }

        public void RenamePlaylist(string oldName, string newName)
        {
            if (MyHome.Instance.SongsManager.RenamePlaylist(oldName, newName))
            {
                if (this.ActivePlaylist == oldName)
                    this.activePlaylist = newName; // set activePlaylist, not ActivePlaylist, to avoid song reorder
                MyHome.Instance.SystemChanged = true;
            }
        }

        public void AddSongToPlaylist(string playlistName, string songName)
        {
            if (MyHome.Instance.SongsManager.AddSongToPlaylist(playlistName, songName))
            {
                if (this.ActivePlaylist == playlistName)
                    this.orderedSongs = null;
                MyHome.Instance.SystemChanged = true;
            }
        }

        public void RemoveSongFromPlaylist(string playlistName, string songName)
        {
            if (MyHome.Instance.SongsManager.RemoveSongFromPlaylist(playlistName, songName))
            {
                if (this.ActivePlaylist == playlistName)
                    this.orderedSongs = null;
                MyHome.Instance.SystemChanged = true;
            }
        }


        public void PlaySong(string name)
        {
            name = MyHome.Instance.SongsManager.EnsureSong(name); // ensure song is downloaded
            if (!string.IsNullOrEmpty(name)) // play
            {
                this.SendState(VOLUME_STATE_NAME, this.Volume.ToString()); // set volume on start playing

                var song = this.Songs.Find(s => s.Name == name);
                if (song?.Local == false)
                    this.SetStateAndSend(PLAYING_STATE_NAME, song.Url);
                else
                    this.SetStateAndSend(PLAYING_STATE_NAME, $"{Host}/api/songs/{Uri.EscapeDataString(name)}");
                this.SetStateAndSend(TITLE_STATE_NAME, name);
            }
            else // on empty value - stop
            {
                this.Queue.Clear();
                this.orderedSongs = null;
                this.alarmType = null;
                this.SetStateAndSend(PLAYING_STATE_NAME, "");
                this.SetStateAndSend(TITLE_STATE_NAME, "");
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
                var source = !string.IsNullOrEmpty(this.ActivePlaylist)
                    ? MyHome.Instance.SongsManager.GetPlaylistSongs(this.ActivePlaylist)
                    : MyHome.Instance.SongsManager.Songs.Where(s => s.Exists).ToList();

                Func<SongInfo, int> getRating = !string.IsNullOrEmpty(this.ActivePlaylist)
                    ? s => MyHome.Instance.SongsManager.GetPlaylistSongRating(this.ActivePlaylist, s.Name)
                    : s => s.Rating;

                if (this.Shuffle)
                {
                    var max = source.Select(getRating).DefaultIfEmpty(1).Max();
                    this.orderedSongs = source.OrderByDescending(s => random.NextDouble() + ((double)getRating(s) / max))
                        .Select(s => s.Name).ToList();
                }
                else
                {
                    this.orderedSongs = source.OrderByDescending(getRating).Select(s => s.Name).ToList();
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
                int notAlarmVolume = this.Volume;
                this.Volume = this.AlarmVolume;

                // stop alarm after AlarmDuration minutes
                Task.Delay(TimeSpan.FromMinutes(this.AlarmDuration)).ContinueWith(_ =>
                {
                    if (this.alarmType != null)
                    {
                        this.PlaySong("");
                        this.Volume = notAlarmVolume;
                    }
                });
            }
        }


        protected override bool NewStateReceived(string name, object oldValue, object newValue)
        {
            base.NewStateReceived(name, oldValue, newValue);
            if (name == PLAYING_STATE_NAME && string.IsNullOrEmpty((string)newValue)) // song ended
            {
                if ((string)newValue == "") // should play next song
                {
                    if (this.alarmType != null) // if alarm was playing repeat it
                        this.PlaySong(this.Title);
                    else
                    {
                        MyHome.Instance.SongsManager.IncreaseSongRating(this.Title, this.ActivePlaylist);
                        this.NextSong(this.Title);
                    }
                }
                else
                    this.SetState(TITLE_STATE_NAME, "");
            }
            return name == VOLUME_STATE_NAME && (int)oldValue != (int)newValue; // save only the volume state change
        }
    }
}
