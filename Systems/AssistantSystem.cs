using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MyHome.Systems.Devices.Drivers.Types;
using MyHome.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace MyHome.Systems;

public class AssistantSystem : BaseSystem
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public class HistoryItem
    {
        [UiProperty]
        public string Message { get; set; }
        [UiProperty]
        public DateTime Time { get; set; }
        [UiProperty]
        public bool Response { get; set; }
    }

    [UiProperty]
    public Dictionary<string, string> Operations { get; }  // name / script

    [UiProperty]
    public Dictionary<string, string> ArgumentMapping { get; } // argument / new value

    [UiProperty]
    public Dictionary<string, string> RequestMapping { get; } // request regex / operation name

    [JsonIgnore]
    [UiProperty]
    public List<HistoryItem> History { get; }

    [JsonIgnore]
    [UiProperty]
    public int UnreadHistoryItems { get; set; }


    [UiProperty(true)]
    public string UnknownRequestResponse { get; set; }

    [UiProperty(true)]
    public string DontUnderstandResponse { get; set; }

    [UiProperty(true)]
    public bool Speak { get; set; }


    [JsonIgnore]
    public Dictionary<string, (DateTime time, byte[] data)> SpeakResponses { get; }


    private readonly object helperLock = new();
    private Process helper;
    private DateTime minuteUpdateTime = DateTime.Now;


    // TODO: "compress" request regex by storing words in list and refer only by index
    // TODO: move notifications (upgrade, offline device, etc.) here


    public AssistantSystem()
    {
        this.Operations = new Dictionary<string, string>();
        this.ArgumentMapping = new Dictionary<string, string>();
        this.RequestMapping = new Dictionary<string, string>();
        this.History = new List<HistoryItem>();
        this.UnreadHistoryItems = 0;
        this.UnknownRequestResponse = "Не знам как да отговоря на това. Може да го добавите в Request Mapping на Assistant страницата.";
        this.DontUnderstandResponse = "Съжалявам, но не ви разбрах. Моля повторете!";
        this.Speak = true;

        this.SpeakResponses = new Dictionary<string, (DateTime time, byte[] data)>();
    }

    public override void Setup()
    {
        base.Setup();
        this.helper = Utils.Utils.StartProcess("python3", "../External/assistantHelper.py", Models.Config.BinPath, false, logger);
    }

    public override void Stop()
    {
        base.Stop();
        if (!this.helper.HasExited)
        {
            this.helper.StandardInput.WriteLine("exit"); // stop helper process
            if (!this.helper.HasExited)
                this.helper.Kill();
        }
    }

    protected override void Update()
    {
        base.Update();

        if (DateTime.Now - this.minuteUpdateTime < TimeSpan.FromMinutes(1))
        {
            Thread.Sleep(100);
            return;
        }
        this.minuteUpdateTime = DateTime.Now;

        // delete speak responses older than 1 minute
        var toDelete = this.SpeakResponses.Where(kvp => kvp.Value.time + TimeSpan.FromMinutes(1) < DateTime.Now).Select(kvp => kvp.Key);
        foreach (var key in toDelete)
            this.SpeakResponses.Remove(key);
    }

    // TODO: actions on event (e.g. security alarm activated)

    public void ProcessRequest(string request)
    {
        if (string.IsNullOrEmpty(request))
            return;

        this.AddToHistory(request, false);

        Regex regex;
        string result = null;
        string roomName = null;
        foreach (var op in this.RequestMapping)
        {
            regex = new Regex(op.Key);
            var match = regex.Match(request.ToLower().Trim());
            if (!match.Success)
                continue;

            if (!this.Operations.ContainsKey(op.Value))
            {
                logger.Warn($"Request {op.Key} has invalid operation: {op.Value}");
                continue;
            }

            var args = match.Groups.Values.Where(g => g.Index != 0).ToDictionary(g => g.Name, g => g.Value);
            args = args.ToDictionary(kvp => kvp.Key, kvp => this.ArgumentMapping.TryGetValue(kvp.Value, out var value) ? value : kvp.Value);
            result = this.ExecuteOperation(op.Value, args) ?? "Има проблем с изпълнението на операцията.";
            roomName = args.TryGetValue("room", out var value) ? value : null;
            break;
        }
        if (string.IsNullOrEmpty(result))
            result = this.UnknownRequestResponse;

        if (this.Speak)
            this.SpeakResponse(result, roomName);

        this.AddToHistory(result, true);
        MyHome.Instance.Events.Fire(this, GlobalEventTypes.AssistantResponse, result);
    }

    public string ExecuteOperation(string operation, string args)
    {
        var parsedArgs = JsonConvert.DeserializeObject<Dictionary<string, string>>(args);
        return this.ExecuteOperation(operation, parsedArgs);
    }

    public void ProcessRecord(byte[] data)
    {
        var result = this.Transcribe(data);
        var request = string.Join("", result.Select(i => i[2]));
        if (string.IsNullOrEmpty(request))
        {
            this.AddToHistory(this.DontUnderstandResponse, true);
            MyHome.Instance.Events.Fire(this, GlobalEventTypes.AssistantResponse, this.DontUnderstandResponse);
        }
        else
            this.ProcessRequest(request);
    }


    private string ExecuteOperation(string operation, Dictionary<string, string> args)
    {
        if (!this.Operations.TryGetValue(operation, out var value))
            return null;

        var script = Regex.Replace(value, @" as \w*", ""); // remove " as <Type>" casts
        logger.Trace($"Execute script: {script}");
        string result = null;
        MyHome.Instance.ExecuteJint(jint => result = jint.SetValue("args", args).Evaluate($"{{ {script} }}").ToString());
        return result;
    }

    private void AddToHistory(string item, bool response)
    {
        this.History.Add(new HistoryItem
        {
            Message = item,
            Time = DateTime.Now,
            Response = response
        });
        this.UnreadHistoryItems++;
        if (this.History.Count > 100)
            this.History.RemoveRange(0, this.History.Count - 50);
    }

    private JArray Transcribe(byte[] data)
    {
        if (Monitor.TryEnter(this.helperLock, TimeSpan.FromSeconds(10)))
        {
            try
            {
                if (this.helper.HasExited)
                    this.helper.Start();

                this.helper.StandardInput.WriteLine("transcribe");
                this.helper.StandardInput.WriteLine(Convert.ToBase64String(data));
                var result = this.helper.StandardOutput.ReadLine();
                if (result == null)
                {
                    this.helper.Kill();
                    return null;
                }

                result = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result[2..(result.Length - 1)])); // remove b' '
                return JArray.Parse(result);
            }
            finally
            {
                Monitor.Exit(this.helperLock);
            }
        }
        this.helper.Kill();
        return null;
    }

    private void SpeakResponse(string response, string roomName)
    {
        var room = MyHome.Instance.Rooms.Find(r => r.Name.Replace(" ", "") == roomName);
        var speaker = room?.Devices.OfType<ISpeakerDriver>().FirstOrDefault();
        if (speaker == null)
        {
            logger.Warn($"Cannot speak the response in room '{roomName}'");
            return;
        }

        if (Monitor.TryEnter(this.helperLock, TimeSpan.FromSeconds(10)))
        {
            try
            {
                if (this.helper.HasExited)
                    this.helper.Start();

                this.helper.StandardInput.WriteLine("synthesize");
                this.helper.StandardInput.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(response)));
                var result = this.helper.StandardOutput.ReadLine();
                if (result == null)
                {
                    this.helper.Kill();
                    return;
                }

                var bytes = Convert.FromBase64String(result[2..(result.Length - 1)]); // remove b' '
                this.SpeakResponses[response.GetHashCode().ToString() + ".wav"] = (DateTime.Now, bytes);

                speaker.PlaySong(response.GetHashCode().ToString() + ".wav");
            }
            finally
            {
                Monitor.Exit(this.helperLock);
            }
        }
        else
            this.helper.Kill();
    }
}