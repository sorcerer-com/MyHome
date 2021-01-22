using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using LibVLCSharp.Shared;

using NLog;

namespace MyHome.Systems
{
    public class MediaPlayerSystem : BaseSystem
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private static readonly string[] supportedFormats = {
            ".mkv", ".avi", ".mov", ".wmv", ".mp4", ".mpg", ".mpeg", ".m4v", ".3gp", ".mp3" };


        public int Volume { get; set; }

        public List<string> MediaPaths { get; }

        public List<string> Radios { get; }

        public List<string> Watched { get; }


        public List<string> MediaList => this.GetMediaList();

        private string playing;
        public string Playing
        {
            get
            {
                if (this.player.State == VLCState.Ended || this.player.State == VLCState.Stopped)
                    this.playing = "";
                return this.playing;
            }
        }

        public string TimeDetails => this.GetTimeDetails();

        private readonly LibVLC libVLC;
        private readonly MediaPlayer player;


        public MediaPlayerSystem(MyHome owner) : base(owner)
        {
            this.Volume = 50;
            this.MediaPaths = new List<string> { "." };
            this.Radios = new List<string>();
            this.Watched = new List<string>();

            this.libVLC = new LibVLC();
            this.player = new MediaPlayer(this.libVLC); // TODO: media list player
            this.playing = "";
        }

        public void Play(string path)
        {
            logger.Debug($"Play media: {path}");
            if (string.IsNullOrEmpty(path))
                return;

            this.playing = path;
            if (!path.StartsWith("http")) // if not URL remove local/shared/radios prefix
                path = path[(path.IndexOf(":") + 1)..];

            this.player.Media = new Media(this.libVLC, new Uri(path));
            this.player.Volume = this.Volume;
            this.player.Fullscreen = true;
            this.player.Play();
            this.MarkWatched(this.playing);
        }

        public override void Stop()
        {
            logger.Debug($"Stop media: {this.playing}");
            this.player.Stop();
        }

        public void Pause()
        {
            logger.Debug($"Pause media: {this.playing}");
            this.player.Pause();
        }

        public void VolumeDown()
        {
            logger.Debug($"Volume down media: {this.playing}");
            this.Volume -= 5;
            this.player.Volume = this.Volume;
            this.Owner.SystemChanged = true;
        }

        public void VolumeUp()
        {
            logger.Debug($"Volume up media: {this.playing}");
            this.Volume += 5;
            this.player.Volume = this.Volume;
            this.Owner.SystemChanged = true;
        }

        public void SeekBack()
        {
            logger.Debug($"Seek back media: {this.playing}");
            this.player.Time -= 30 * 1000; // -30 seconds
        }

        public void SeekForward()
        {
            logger.Debug($"Seek forward media: {this.playing}");
            this.player.Time += 30 * 1000; // +30 seconds
        }

        public void SeekBackFast()
        {
            logger.Debug($"Seek back fast media: {this.playing}");
            this.player.Time -= 600 * 1000; // -600 seconds
        }

        public void SeekForwardFast()
        {
            logger.Debug($"Seek forward fast media: {this.playing}");
            this.player.Time += 600 * 1000; // +600 seconds
        }


        private List<string> GetMediaList()
        {
            var result = new List<string>();
            for (int i = 0; i < this.MediaPaths.Count; i++)
            {
                if (!Directory.Exists(this.MediaPaths[i]))
                    continue;

                var filePaths = Directory.EnumerateFiles(this.MediaPaths[i], "*.*", SearchOption.AllDirectories)
                    .Where(p => supportedFormats.Any(f => p.EndsWith(f)));
                result.AddRange(filePaths.Select(p => $"local{i + 1}:" + p));
            }
            result.Sort();
            result.AddRange(this.Radios.Select(r => "radios:" + r));
            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0042:Deconstruct variable declaration")]
        private string GetTimeDetails()
        {
            static (long hours, long minutes, long seconds) GetTime(long millis)
            {
                var s = millis / 100;
                var m = Math.DivRem(s, 60, out s);
                var h = Math.DivRem(m, 60, out m);
                return (hours: h, minutes: m, seconds: s);
            }

            var time = GetTime(this.player.Time);
            var length = GetTime(this.player.Length);
            return $"{time.minutes:00}:{time.seconds:00} / {length.minutes:00}:{length.seconds:00}";
        }

        private void MarkWatched(string path)
        {
            logger.Debug($"Mark as watched: {path}");

            if (!this.Watched.Contains(path))
                this.Watched.Add(path);

            // cleanup watched list from nonexistent files
            var mediaList = this.GetMediaList();
            this.Watched.RemoveAll(w => !mediaList.Contains(w));
            this.Owner.SystemChanged = true;
        }
    }
}
