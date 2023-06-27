using System.Collections.Generic;
using System.IO;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Models;

public class SongsManager
{
    private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

    public sealed class SongInfo
    {
        [UiProperty]
        public string Name { get; set; }
        [UiProperty]
        public string Url { get; set; }
        [UiProperty]
        public int Rating { get; set; }
        [JsonIgnore]
        [UiProperty]
        public bool Exists => File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, this.Name));
    }

    // TODO: remove once move from MediaPlayer
    private List<SongInfo> songs;
    public List<SongInfo> Songs
    {
        get
        {
            this.songs ??= MyHome.Instance.MediaPlayerSystem.songsList
                    .Select(s => new SongInfo { Name = s.Name, Url = s.Url, Rating = s.Rating }).ToList();
            return this.songs;
        }
    }


    public SongsManager()
    {
        // TODO: this.Songs = new List<SongInfo>();

        Directory.CreateDirectory(MyHome.Instance.Config.SongsPath);
    }

    public string AddSong(string url)
    {
        logger.Debug($"Add song: {url}");
        string name = url;
        if (url.Contains("youtube") || url.Contains("youtu.be"))
            name = Services.DownloadYouTubeAudioAsync(url, MyHome.Instance.Config.SongsPath).Result;
        else if (!name.StartsWith("http"))
            return null;
        if (name == null)
            return null;

        if (!this.Songs.Any(s => s.Name == name))
            this.Songs.Add(new SongInfo() { Name = name, Url = url, Rating = this.Songs.Select(s => s.Rating).Max() + 1 });

        // cleanup songs if we exceed the usage capacity
        Utils.Utils.CleanupFilesByCapacity(this.Songs.OrderBy(s => s.Rating)
            .Select(s => Path.Join(MyHome.Instance.Config.SongsPath, s.Name))
                .Where(p => File.Exists(p)).Select(p => new FileInfo(p)),
            MyHome.Instance.Config.SongsDiskUsage, logger);
        return name;
    }

    public string EnsureSong(string name)
    {
        if (string.IsNullOrEmpty(name) || File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, name)))
            return name;

        var song = this.Songs.FirstOrDefault(s => s.Name == name);
        if (song == null)
            return name;
        if (song.Url.Contains("youtube") || song.Url.Contains("youtu.be"))
            song.Name = Services.DownloadYouTubeAudioAsync(song.Url, MyHome.Instance.Config.SongsPath).Result ?? song.Name;
        return song.Name;
    }

    public void IncreaseSongRating(string name, int rating = 1)
    {
        var song = this.Songs.FirstOrDefault(s => s.Name == name);
        if (song != null)
            song.Rating += rating;
    }
}
