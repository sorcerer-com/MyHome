using LibVLCSharp.Shared;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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


        public List<string> MediaList => GetMediaList();

        private string playing;
        public string Playing
        {
            get
            {
                if (player.State == VLCState.Ended || player.State == VLCState.Stopped)
                    playing = "";
                return playing;
            }
        }

        public string TimeDetails => GetTimeDetails();

        private readonly LibVLC libVLC;
        private readonly MediaPlayer player;


        public MediaPlayerSystem(MyHome owner) : base(owner)
        {
            Volume = 50;
            MediaPaths = new List<string> { "." };
            Radios = new List<string>();
            Watched = new List<string>();

            libVLC = new LibVLC();
            player = new MediaPlayer(libVLC); // TODO: media list player
            playing = "";
        }

        public void Play(string path)
        {
            logger.Debug($"Play media: {path}");
            if (string.IsNullOrEmpty(path))
                return;

            playing = path;
            if (!path.StartsWith("http")) // if not URL remove local/shared/radios prefix
                path = path[(path.IndexOf(":") + 1)..];

            player.Media = new Media(libVLC, new Uri(path));
            player.Volume = Volume;
            player.Fullscreen = true;
            player.Play();
            MarkWatched(playing);
        }

        public override void Stop()
        {
            logger.Debug($"Stop media: {playing}");
            player.Stop();
        }

        public void Pause()
        {
            logger.Debug($"Pause media: {playing}");
            player.Pause();
        }

        public void VolumeDown()
        {
            logger.Debug($"Volume down media: {playing}");
            Volume -= 5;
            player.Volume = Volume;
            Owner.SystemChanged = true;
        }

        public void VolumeUp()
        {
            logger.Debug($"Volume up media: {playing}");
            Volume += 5;
            player.Volume = Volume;
            Owner.SystemChanged = true;
        }

        public void SeekBack()
        {
            logger.Debug($"Seek back media: {playing}");
            player.Time -= 30 * 1000; // -30 seconds
        }

        public void SeekForward()
        {
            logger.Debug($"Seek forward media: {playing}");
            player.Time += 30 * 1000; // +30 seconds
        }

        public void SeekBackFast()
        {
            logger.Debug($"Seek back fast media: {playing}");
            player.Time -= 600 * 1000; // -600 seconds
        }

        public void SeekForwardFast()
        {
            logger.Debug($"Seek forward fast media: {playing}");
            player.Time += 600 * 1000; // +600 seconds
        }


        private List<string> GetMediaList()
        {
            var result = new List<string>();
            for (int i = 0; i < MediaPaths.Count; i++)
            {
                if (!Directory.Exists(MediaPaths[i]))
                    continue;

                var filePaths = Directory.EnumerateFiles(MediaPaths[i], "*.*", SearchOption.AllDirectories)
                    .Where(p => supportedFormats.Any(f => p.EndsWith(f)));
                result.AddRange(filePaths.Select(p => $"local{i + 1}:" + p));
            }
            result.Sort();
            result.AddRange(Radios.Select(r => "radios:" + r));
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

            var time = GetTime(player.Time);
            var length = GetTime(player.Length);
            return $"{time.minutes:00}:{time.seconds:00} / {length.minutes:00}:{length.seconds:00}";
        }

        private void MarkWatched(string path)
        {
            logger.Debug($"Mark as watched: {path}");

            if (!Watched.Contains(path))
                Watched.Add(path);

            // cleanup watched list from nonexistent files
            var mediaList = GetMediaList();
            Watched.RemoveAll(w => !mediaList.Contains(w));
            Owner.SystemChanged = true;
        }
    }
}
