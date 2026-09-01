using System.Collections.Generic;
using System.IO;
using System.Linq;

using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Models;

public class SongsManager
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

    public sealed class Playlist
    {
        [UiProperty]
        public string Name { get; set; }
        [UiProperty]
        public Dictionary<string, int> Songs { get; set; } = new Dictionary<string, int>(); // song name -> rating
    }

    public List<SongInfo> Songs { get; }

    public List<Playlist> Playlists { get; }


    public SongsManager()
    {
        this.Songs = new List<SongInfo>();
        this.Playlists = new List<Playlist>();

        Directory.CreateDirectory(MyHome.Instance.Config.SongsPath);
    }

    public string AddSong(string url)
    {
        logger.Info($"Add song: {url}");
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
        {
            var rating = this.Songs.Select(s => s.Rating).DefaultIfEmpty(0).Max();
            this.Songs.Add(new SongInfo() { Name = name, Url = url, Rating = rating, Local = local });
        }

        // cleanup songs if we exceed the usage capacity
        Utils.Utils.CleanupFilesByCapacity(this.Songs.OrderBy(s => s.Rating)
            .Select(s => Path.Join(MyHome.Instance.Config.SongsPath, s.Name))
                .Where(File.Exists).Select(p => new FileInfo(p)),
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

        foreach (var playlist in this.Playlists)
        {
            if (playlist.Songs.Remove(oldName, out var rating))
                playlist.Songs[newName] = rating;
        }
        return true;
    }

    public bool ChangeSongUrl(string name, string newUrl)
    {
        logger.Debug($"Change song '{name}' url to '{newUrl}'");
        if (!newUrl.StartsWith("http"))
            return false;

        var song = this.Songs.Find(s => s.Name == name);
        if (song == null)
            return false;

        song.Url = newUrl;
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
        {
            this.Songs.Remove(song);
            foreach (var playlist in this.Playlists)
                playlist.Songs.Remove(name);
        }
        return true;
    }

    public string EnsureSong(string name)
    {
        if (string.IsNullOrEmpty(name) || File.Exists(Path.Join(MyHome.Instance.Config.SongsPath, name)) ||
            File.Exists(Path.Join(Config.SoundsPath, name)))
            return name;

        var song = this.Songs.Find(s => s.Name == name);
        if (song == null)
            return name;
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

    public void IncreaseSongRating(string songName, string playlistName = null, int rating = 1)
    {
        if (string.IsNullOrEmpty(playlistName))
        {
            var song = this.Songs.Find(s => s.Name == songName);
            if (song != null)
                song.Rating += rating;
        }
        else
        {
            var playlist = this.Playlists.Find(p => p.Name == playlistName);
            if (playlist == null || !playlist.Songs.ContainsKey(songName))
                return;
            playlist.Songs[songName] += rating;
        }
    }

    public int GetPlaylistSongRating(string playlistName, string songName)
    {
        var playlist = this.Playlists.Find(p => p.Name == playlistName);
        if (playlist == null)
            return 0;
        return playlist.Songs.TryGetValue(songName, out var r) ? r : 0;
    }


    public bool CreatePlaylist(string name)
    {
        if (string.IsNullOrEmpty(name) || this.Playlists.Exists(p => p.Name == name))
            return false;

        logger.Debug($"Create playlist: {name}");
        this.Playlists.Add(new Playlist { Name = name });
        return true;
    }

    public bool DeletePlaylist(string name)
    {
        var playlist = this.Playlists.Find(p => p.Name == name);
        if (playlist == null)
            return false;

        logger.Debug($"Delete playlist: {name}");
        this.Playlists.Remove(playlist);
        return true;
    }

    public bool RenamePlaylist(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(newName) || this.Playlists.Exists(p => p.Name == newName))
            return false;

        var playlist = this.Playlists.Find(p => p.Name == oldName);
        if (playlist == null)
            return false;

        logger.Debug($"Rename playlist '{oldName}' to '{newName}'");
        playlist.Name = newName;
        return true;
    }

    public bool AddSongToPlaylist(string playlistName, string songName)
    {
        var playlist = this.Playlists.Find(p => p.Name == playlistName);
        if (playlist == null || !this.Songs.Exists(s => s.Name == songName))
            return false;

        if (playlist.Songs.ContainsKey(songName))
            return false;

        logger.Debug($"Add song '{songName}' to playlist '{playlistName}'");
        playlist.Songs[songName] = playlist.Songs.Values.DefaultIfEmpty(0).Max();
        return true;
    }

    public bool RemoveSongFromPlaylist(string playlistName, string songName)
    {
        var playlist = this.Playlists.Find(p => p.Name == playlistName);
        if (playlist == null)
            return false;

        logger.Debug($"Remove song '{songName}' from playlist '{playlistName}'");
        return playlist.Songs.Remove(songName);
    }

    public List<SongInfo> GetPlaylistSongs(string playlistName)
    {
        var playlist = this.Playlists.Find(p => p.Name == playlistName);
        if (playlist == null)
            return new List<SongInfo>();

        return playlist.Songs.Keys
            .Select(name => this.Songs.Find(s => s.Name == name))
            .Where(s => s != null)
            .ToList();
    }


    private static bool IsYoutubeUrl(string url)
    {
        return url.StartsWith("http") && (url.Contains("youtube") || url.Contains("youtu.be"));
    }
}
