using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using MailKit.Net.Smtp;

using MimeKit;

using Newtonsoft.Json.Linq;

using NLog;

using ScrapySharp.Html.Forms;
using ScrapySharp.Network;

using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace MyHome.Utils
{
    public static class Services
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        public static bool SendEMail(string server, string sender, string password,
            string recipient, string subject, string content, List<string> fileNames = null)
        {
            try
            {
                logger.Debug($"Send email to '{recipient}' subject: '{subject}' ({fileNames?.Count} files)");

                var mail = new MimeMessage();
                mail.From.Add(MailboxAddress.Parse("sorcerer_com@abv.bg"));
                mail.To.Add(MailboxAddress.Parse("sorcerer_com@abv.bg"));
                mail.Subject = subject;

                var builder = new BodyBuilder { TextBody = content };
                fileNames?.ForEach(f => builder.Attachments.Add(f));
                mail.Body = builder.ToMessageBody();

                var (host, port) = Utils.SplitAddress(server);
                using var smtp = new SmtpClient { Timeout = 30 * 1000 };
                smtp.Connect(host, port ?? 465, port == null || port == 465 || port == 587);
                smtp.Authenticate(sender, password);
                smtp.Send(mail);
                smtp.Disconnect(true);

                return true;
            }
            catch (Exception e)
            {
                logger.Error($"Cannot send email to '{recipient}' subject: '{subject}'");
                logger.Debug(e);
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S1075")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S1854")]
        public static bool SendSMS(string number, string provider, string password, string message)
        {
            try
            {
                logger.Debug($"Send SMS '{message.Replace("\n", " ")}' to {number}");
                if (provider.ToLower() == "telenor")
                {
                    var browser = new ScrapingBrowser { Encoding = Encoding.UTF8, IgnoreCookies = true };
                    var page = browser.NavigateToPage(new Uri("https://my.yettel.bg"));
                    // login
                    var form = new PageWebForm(page.Find("form", ScrapySharp.Html.By.Class("form")).First(), browser);
                    form["phone"] = number[1..];
                    page = form.Submit(new Uri("https://id.yettel.bg/id/signin-switchable/"));
                    form = new PageWebForm(page.Find("form", ScrapySharp.Html.By.Class("form")).First(), browser);
                    form["pin"] = password;
                    page = form.Submit(new Uri("https://id.yettel.bg/id/verify-phone/"));
                    // go to sms
                    page = browser.NavigateToPage(new Uri("https://my.yettel.bg/compose"));
                    // sms
                    form = page.FindFormById("new-sms-form");
                    form["receiverPhoneNum"] = number;
                    if (message.Length > 99)
                        message = message[..99];
                    form.FormFields.Add(new FormField { Name = "txtareaMessage", Value = message });
                    try
                    {
                        page = form.Submit(new Uri("https://my.yettel.bg/st/validatesms"));
                    }
                    catch (Exception e)
                    {
                        // Expected exception - response 302 Found
                        if (e.InnerException is not WebException)
                            throw;
                    }
                    browser.NavigateToPage(new Uri("https://my.yettel.bg/logout"));
                    return true;
                }
                logger.Error($"Cannot send SMS - invalid operator '{provider}'");
                return false;
            }
            catch (Exception e)
            {
                logger.Error($"Cannot send SMS '{message.Replace("\n", " ")}' to {number}");
                logger.Debug(e);
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S1168")]
        public static JToken GetJsonContent(string url)
        {
            try
            {
                logger.Trace($"Get json content from '{url}'");
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                var json = client.GetStringAsync(url).Result;

                return JToken.Parse(json);
            }
            catch (Exception e)
            {
                logger.Trace(e, $"Cannot get json content from '{url}'");
                return null;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S112")]
        public static async Task<string> DownloadYouTubeAudioAsync(string url, string path)
        {
            try
            {
                logger.Debug($"Getting YouTube video details: {url}");
                var youtube = new YoutubeClient();
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
                var streamInfo = streamManifest
                    .GetAudioOnlyStreams()
                    .Where(s => s.Container == Container.Mp4 || s.Container == Container.Mp3)
                    .GetWithHighestBitrate();

                // if path doesn't contains filename
                if (path.LastIndexOf('.') <= 0) // if it is not "."
                {
                    var video = await youtube.Videos.GetAsync(url);
                    // escape invalid symbols
                    var filename = new string(video.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '\"' || c == '?' ? '_' : c).ToArray());
                    path = Path.Join(path, $"{filename}.{streamInfo.Container}");
                }

                logger.Debug($"Downloading YouTube video '{url}' to '{path}'");
                await youtube.Videos.Streams.DownloadAsync(streamInfo, path);

                // extract mp3 from the mp4
                if (streamInfo.Container == Container.Mp4)
                {
                    logger.Debug($"Converting mp4 file to mp3");
                    var ffmpegProcess = new System.Diagnostics.Process();
                    ffmpegProcess.StartInfo.UseShellExecute = false;
                    ffmpegProcess.StartInfo.RedirectStandardInput = true;
                    ffmpegProcess.StartInfo.RedirectStandardOutput = true;
                    ffmpegProcess.StartInfo.RedirectStandardError = true;
                    ffmpegProcess.StartInfo.CreateNoWindow = true;
                    ffmpegProcess.StartInfo.FileName = "ffmpeg";
                    ffmpegProcess.StartInfo.Arguments = $" -i \"{path}\" -vn -f mp3 -y \"{path.Replace(".mp4", ".mp3")}\"";
                    ffmpegProcess.Start();
                    await ffmpegProcess.WaitForExitAsync();
                    if (ffmpegProcess.ExitCode != 0)
                        throw new Exception(ffmpegProcess.StandardError.ReadToEnd());
                    File.Delete(path); // delete the mp4
                }
                return Path.GetFileName(path.Replace(".mp4", ".mp3"));
            }
            catch (Exception e)
            {
                logger.Error($"Cannot download YouTube audio: {url}");
                logger.Debug(e);
                return null;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S112")]
        public static bool CreateVideo(List<string> imageFilePaths, string outputFilePath)
        {
            if (imageFilePaths.Count == 0)
            {
                logger.Debug($"Skip creating video '{outputFilePath}' from {imageFilePaths.Count} images");
                return false;
            }

            logger.Debug($"Creating video '{outputFilePath}' from {imageFilePaths.Count} images");
            try
            {
                var inputFilePath = Path.Join(Path.GetDirectoryName(outputFilePath), "list.txt");
                File.WriteAllText(inputFilePath, string.Join("\n", imageFilePaths.Select(f => $"file '{f}'\nduration 1")));

                var ffmpegProcess = new System.Diagnostics.Process();
                ffmpegProcess.StartInfo.UseShellExecute = false;
                ffmpegProcess.StartInfo.RedirectStandardInput = true;
                ffmpegProcess.StartInfo.RedirectStandardOutput = true;
                ffmpegProcess.StartInfo.RedirectStandardError = true;
                ffmpegProcess.StartInfo.CreateNoWindow = true;
                ffmpegProcess.StartInfo.FileName = "ffmpeg";
                ffmpegProcess.StartInfo.Arguments = $" -f concat -safe 0 -i \"{inputFilePath}\" -y \"{outputFilePath}\"";
                ffmpegProcess.Start();

                var stdErr = new StringBuilder();
                while (!ffmpegProcess.HasExited) // we need to start reading the output ffmpeg to start processing
                    stdErr.Append(ffmpegProcess.StandardError.ReadToEnd());
                File.Delete(inputFilePath);
                if (ffmpegProcess.ExitCode != 0)
                    throw new Exception(stdErr.ToString());
                return true;
            }
            catch (Exception e)
            {
                logger.Error($"Failed to create video '{outputFilePath}' from {imageFilePaths.Count} images");
                logger.Debug(e);
                return false;
            }
        }
    }
}
