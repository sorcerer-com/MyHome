using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using LibVLCSharp.Shared;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems
{
    public class MediaPlayerSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private static readonly string[] supportedFormats = {
            ".mkv", ".avi", ".mov", ".wmv", ".mp4", ".mpg", ".mpeg", ".m4v", ".3gp", ".mp3" };

        public static readonly string SongsPath = Path.Combine(Config.BinPath, "Songs");


        [UiProperty(true)]
        public List<string> MediaPaths { get; }

        [UiProperty(true)]
        public List<string> Radios { get; }

        [UiProperty(true, "MB")]
        public double SongsDiskUsage { get; set; } // MB


        private bool sortByDate;
        [UiProperty]
        public bool SortByDate
        {
            get => this.sortByDate;
            set
            {
                if (this.sortByDate != value)
                {
                    this.sortByDate = value;
                    this.RefreshMediaList();
                }
            }
        }

        [UiProperty]
        public int Volume { get; set; }

        [UiProperty]
        public List<string> Watched { get; }

        public Dictionary<string, int> Songs { get; } // song path / play count


        [JsonIgnore]
        [UiProperty]
        public List<string> MediaList => this.GetMediaList();

        private string playing;
        [JsonIgnore]
        [UiProperty]
        public string Playing
        {
            get
            {
                if (this.player.State == VLCState.Stopped)
                    this.playing = "";
                return this.playing;
            }
        }

        [JsonIgnore]
        [UiProperty]
        public bool Paused => this.player.State == VLCState.Paused;

        [JsonIgnore]
        [UiProperty]
        public string TimeDetails => this.GetTimeDetails();

        private readonly List<string> mediaList;
        private DateTime lastRefreshMediaListTimer;
        private readonly LibVLC libVLC;
        private readonly MediaPlayer player;


        public MediaPlayerSystem()
        {
            this.MediaPaths = new List<string>();
            this.Radios = new List<string>();
            this.SongsDiskUsage = 500;
            this.sortByDate = false;
            this.Volume = 50;
            this.Watched = new List<string>();
            this.Songs = new Dictionary<string, int>();

            this.mediaList = new List<string>();
            this.lastRefreshMediaListTimer = DateTime.Now - TimeSpan.FromDays(1);
            this.libVLC = new LibVLC();
            this.player = new MediaPlayer(this.libVLC);
            this.playing = "";

            Directory.CreateDirectory(SongsPath);
        }

        public override void Stop()
        {
            base.Stop();
            this.StopMedia();
        }

        protected override void Update()
        {
            base.Update();

            // stop if end reached
            if (this.player.State == VLCState.Ended)
                this.player.Stop();
            System.Threading.Thread.Sleep(100);
        }


        public void Play(string path)
        {
            logger.Debug($"Play media: {path}");
            if (string.IsNullOrEmpty(path))
                return;

            this.playing = path;
            if (!path.StartsWith("http")) // if not URL remove media/radios prefix
                path = path[(path.IndexOf(":") + 1)..];

            this.player.Media = new Media(this.libVLC, new Uri(path));
            this.player.Media.AddOption(":subsdec-encoding=Windows-1251"); // set default encoding to Cyrillic
            this.player.Volume = this.Volume;
            this.player.Fullscreen = true;
            this.player.Play();
            this.MarkWatched(this.playing);
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaPlayed, this.playing);
        }

        public void StopMedia()
        {
            logger.Debug($"Stop media: {this.playing}");
            this.player.Stop();
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaStopped);
        }

        public void Pause()
        {
            logger.Debug($"Pause media: {this.playing}");
            this.player.Pause();
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaPaused);
        }

        public void VolumeDown()
        {
            logger.Debug($"Volume down media: {this.playing} to {this.Volume - 5}");
            this.Volume -= 5;
            this.player.Volume = this.Volume;
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaVolumeDown);
        }

        public void VolumeUp()
        {
            logger.Debug($"Volume up media: {this.playing} to {this.Volume + 5}");
            this.Volume += 5;
            this.player.Volume = this.Volume;
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaVolumeUp);
        }

        public void SeekBack()
        {
            logger.Debug($"Seek back media: {this.playing}");
            this.player.Time -= 30 * 1000; // -30 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekBack);
        }

        public void SeekForward()
        {
            logger.Debug($"Seek forward media: {this.playing}");
            this.player.Time += 30 * 1000; // +30 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekForward);
        }

        public void SeekBackFast()
        {
            logger.Debug($"Seek back fast media: {this.playing}");
            this.player.Time -= 600 * 1000; // -600 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekBackFast);
        }

        public void SeekForwardFast()
        {
            logger.Debug($"Seek forward fast media: {this.playing}");
            this.player.Time += 600 * 1000; // +600 seconds
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.MediaSeekForwardFast);
        }

        public void RefreshMediaList()
        {
            this.lastRefreshMediaListTimer = DateTime.Now - TimeSpan.FromDays(1);
        }

        public string AddSong(string url)
        {
            logger.Debug($"Add song: {url}");
            string filepath = url;
            if (url.Contains("youtube"))
                filepath = Services.DownloadYouTubeAudioAsync(url, SongsPath).Result;
            if (!this.Songs.ContainsKey(filepath))
                this.Songs.Add(filepath, 0);
            // cleanup songs if we exceed the usage capacity
            Utils.Utils.CleanupFilesByCapacity(this.Songs.OrderBy(kvp => kvp.Value).Select(kvp => new FileInfo(Path.Join(SongsPath, kvp.Key))),
                this.SongsDiskUsage, logger);
            foreach (var file in this.Songs.Keys.Where(s => !File.Exists(Path.Join(SongsPath, s))))
                this.Songs.Remove(file);
            return filepath;
        }


        private List<string> GetMediaList()
        {
            lock (this.mediaList)
            {
                if (DateTime.Now > this.lastRefreshMediaListTimer + TimeSpan.FromDays(1))
                {
                    logger.Debug("Refresh media list");
                    this.lastRefreshMediaListTimer = DateTime.Now;
                    this.mediaList.Clear();
                    for (int i = 0; i < this.MediaPaths.Count; i++)
                    {
                        if (!Directory.Exists(this.MediaPaths[i]))
                            continue;

                        var filePaths = Directory.EnumerateFiles(this.MediaPaths[i], "*.*", SearchOption.AllDirectories)
                            .Where(p => supportedFormats.Any(f => p.EndsWith(f)));
                        this.mediaList.AddRange(filePaths.Select(p => $"media{i + 1}:" + p));
                    }
                    if (this.sortByDate)
                    {
                        this.mediaList.Sort((a, b) => Directory.GetLastWriteTime(a[(a.IndexOf(":") + 1)..])
                            .CompareTo(Directory.GetLastWriteTime(b[(b.IndexOf(":") + 1)..])));
                    }
                    else
                        this.mediaList.Sort();
                    this.mediaList.AddRange(this.Radios.Select(r => "radios:" + r));
                }
            }
            return this.mediaList;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0042:Deconstruct variable declaration")]
        private string GetTimeDetails()
        {
            static (long hours, long minutes, long seconds) GetTime(long millis)
            {
                var s = millis / 1000;
                var m = Math.DivRem(s, 60, out s);
                var h = Math.DivRem(m, 60, out m);
                return (hours: h, minutes: m, seconds: s);
            }

            var time = GetTime(this.player.Time);
            var length = GetTime(this.player.Length);
            return $"{time.hours:00}:{time.minutes:00} / {length.hours:00}:{length.minutes:00}";
        }

        private void MarkWatched(string path)
        {
            logger.Debug($"Mark as watched: {path}");

            if (!this.Watched.Contains(path))
                this.Watched.Add(path);

            // cleanup watched list from nonexistent files
            var mediaList = this.GetMediaList();
            this.Watched.RemoveAll(w => !mediaList.Contains(w));
            MyHome.Instance.SystemChanged = true;
        }
    }
}
