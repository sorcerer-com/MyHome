using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using MyHome.Utils;

using Newtonsoft.Json;

using NLog;

namespace MyHome.Systems;

public class AssistantSystem : BaseSystem
{
    private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

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
    }

    // TODO: actions on event (e.g. security alarm activated)

    public void ProcessRequest(string request)
    {
        if (string.IsNullOrEmpty(request))
            return;

        this.AddToHistory(request, false);

        Regex regex;
        string result = null;
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
            args = args.ToDictionary(kvp => kvp.Key, kvp => this.ArgumentMapping.ContainsKey(kvp.Value) ? this.ArgumentMapping[kvp.Value] : kvp.Value);
            result = this.ExecuteOperation(op.Value, args) ?? "Има проблем с изпълнението на операцията.";
            break;
        }
        if (string.IsNullOrEmpty(result))
            result = this.UnknownRequestResponse;

        this.AddToHistory(result, true);
        MyHome.Instance.Events.Fire(this, GlobalEventTypes.AssistantResponse, result);
    }

    public string ExecuteOperation(string operation, string args)
    {
        var parsedArgs = JsonConvert.DeserializeObject<Dictionary<string, string>>(args);
        return this.ExecuteOperation(operation, parsedArgs);
    }


    private string ExecuteOperation(string operation, Dictionary<string, string> args)
    {
        if (!this.Operations.ContainsKey(operation))
            return null;

        var script = this.Operations[operation];
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
}