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
        [UiProperty]
        public bool Local { get; set; }
        [JsonIgnore]
        [UiProperty]
        public bool Exists => this.Local && File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, this.Name));
    }

    public List<SongInfo> Songs { get; }


    public SongsManager()
    {
        this.Songs = new List<SongInfo>();

        Directory.CreateDirectory(MyHome.Instance.Config.SongsPath);
    }

    public string AddSong(string url)
    {
        logger.Debug($"Add song: {url}");
        if (!url.StartsWith("http"))
            return null;

        string name = url;
        bool local = false;
        if (IsYoutubeUrl(url)) // youtube
        {
            name = Services.DownloadYouTubeAudioAsync(url, MyHome.Instance.Config.SongsPath).Result;
            if (name != null)
            {
                Services.NormalizeAudioVolume(Path.Join(MyHome.Instance.Config.SongsPath, name));
                local = true;
            }
        }
        else if (url.StartsWith("http") && url.EndsWith(".m3u")) // m3u playlist
        {
            var content = Services.GetContent(url);
            foreach (var item in content.Split(new string[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries))
                this.AddSong(item);
            return null;
        }
        if (name == null)
            return null;

        if (!this.Songs.Exists(s => s.Name == name))
            this.Songs.Add(new SongInfo() { Name = name, Url = url, Rating = this.Songs.Select(s => s.Rating).Max() + 1, Local = local });

        // cleanup songs if we exceed the usage capacity
        Utils.Utils.CleanupFilesByCapacity(this.Songs.OrderBy(s => s.Rating)
            .Select(s => Path.Join(MyHome.Instance.Config.SongsPath, s.Name))
                .Where(p => File.Exists(p)).Select(p => new FileInfo(p)),
            MyHome.Instance.Config.SongsDiskUsage, logger);
        return name;
    }

    public bool RenameSong(string oldName, string newName)
    {
        logger.Debug($"Rename song '{oldName}' to '{newName}'");
        var song = this.Songs.Find(s => s.Name == oldName);
        if (song == null)
            return false;

        if (song.Exists)
            File.Move(Path.Join(MyHome.Instance.Config.SongsPath, oldName), Path.Join(MyHome.Instance.Config.SongsPath, newName));
        song.Name = newName;
        return true;
    }

    public bool DeleteSong(string name, bool keepEntry = false)
    {
        logger.Debug($"Delete song: {name}");
        var song = this.Songs.Find(s => s.Name == name);
        if (song == null)
            return false;

        if (song.Exists)
            File.Delete(Path.Join(MyHome.Instance.Config.SongsPath, name));
        if (!keepEntry)
            this.Songs.Remove(song);
        return true;
    }

    public string EnsureSong(string name)
    {
        if (string.IsNullOrEmpty(name) || File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, name)))
            return name;

        var song = this.Songs.Find(s => s.Name == name);
        if (song == null)
            return null;
        if (IsYoutubeUrl(song.Url))
        {
            var oldName = Services.DownloadYouTubeAudioAsync(song.Url, MyHome.Instance.Config.SongsPath).Result ?? song.Name;
            if (oldName != song.Name && File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, oldName)))
                File.Move(Path.Join(MyHome.Instance.Config.SongsPath, oldName), Path.Join(MyHome.Instance.Config.SongsPath, song.Name));
            if (song.Exists)
                Services.NormalizeAudioVolume(Path.Join(MyHome.Instance.Config.SongsPath, song.Name));
        }
        return song.Name;
    }

    public void IncreaseSongRating(string name, int rating = 1)
    {
        var song = this.Songs.Find(s => s.Name == name);
        if (song != null)
            song.Rating += rating;
    }


    private static bool IsYoutubeUrl(string url)
    {
        return url.StartsWith("http") && (url.Contains("youtube") || url.Contains("youtu.be"));
    }
}
