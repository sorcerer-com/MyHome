using System.Net.Http;
using System;
using MyHome.Utils;
using Newtonsoft.Json;
using NLog;
using Newtonsoft.Json.Linq;
using JWT.Algorithms;
using JWT.Builder;
using System.Linq;
using MyHome.Systems.Devices;
using System.Collections.Generic;
using MyHome.Systems;
using MyHome.Systems.Actions;

namespace MyHome.Models;

public class BackupMode
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private const string BackupModeNotification = "Backup Mode";

    private readonly int disconnectedAlertInterval = 1; // minutes
    private DateTime disconnectedTime;
    private DateTime nextConfigSync;


    [UiProperty(true, "Address of the peer MyHome instance")]
    public string PeerServer { get; set; } = "";

    [UiProperty(true, "Determine whether current is the main server instance")]
    public bool MainInstance { get; set; } = true;

    [UiProperty(true, "If true the backup instance will auto upgrade")]
    public bool AutoUpgrade { get; set; } = true;

    [UiProperty(true, "If true the main server configuration will be synced")]
    public bool SyncConfiguration { get; set; } = true;


    // backup mode is enabled if peer is online and current instance is not the main one
    [JsonIgnore]
    public bool Enabled => !this.peerIsOffline && !this.MainInstance;

    private bool peerIsOffline = false;


    public void Setup()
    {
        this.disconnectedTime = DateTime.Now;
        this.nextConfigSync = DateTime.Now.AddMinutes(1);
        if (!string.IsNullOrEmpty(this.PeerServer) && !this.MainInstance)
            MyHome.Instance.AddNotification(BackupModeNotification);
    }

    public void Check()
    {
        if (string.IsNullOrEmpty(this.PeerServer))
            return;

        this.CheckPeerServerStatus();

        if (!this.MainInstance) // backup server
        {
            if (this.SyncConfiguration && DateTime.Now > this.nextConfigSync)
                SyncConfigurations();
            if (this.AutoUpgrade)
                AutoUpgradeBackupServer();
        }
    }

    private void CheckPeerServerStatus()
    {
        var status = GetPeerServerStatus();

        var now = DateTime.Now;
        if (status)
        {
            this.disconnectedTime = now;
            if (this.peerIsOffline)
            {
                logger.Warn($"Peer server instance ({this.PeerServer}) is back online");
                MyHome.Instance.RemoveNotification($"Peer MyHome server ({this.PeerServer}) is down");
                if (!this.MainInstance) // if backup instance
                    MyHome.Instance.AddNotification(BackupModeNotification);

                this.peerIsOffline = false;
            }
        }
        else
        {
            // exit backup mode if no response from main server for 10 seconds
            if (now - this.disconnectedTime > TimeSpan.FromSeconds(10))
            {
                if (this.Enabled)
                {
                    logger.Warn("Main server instance is down. Exit Backup mode.");
                    MyHome.Instance.RemoveNotification(BackupModeNotification);
                }
                this.peerIsOffline = true;
            }

            if (now - this.disconnectedTime > TimeSpan.FromMinutes(this.disconnectedAlertInterval))
            {
                MyHome.Instance.AddNotification($"Peer MyHome server ({this.PeerServer}) is down")
                    .Details($"from {this.disconnectedTime:dd/MM/yyyy HH:mm:ss}!")
                    .Validity(TimeSpan.FromHours(1))
                    .SendAlert(forceSend: true);
            }
        }
    }

    private bool GetPeerServerStatus()
    {
        try
        {
            using var client = Utils.Utils.GetHttpClient(skipCertVerification: true);
            return client.GetAsync($"{this.PeerServer}/api/status").Result.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }


    private void SyncConfigurations()
    {
        logger.Info($"Sync configuration from peer server {this.PeerServer}");
        this.nextConfigSync = this.nextConfigSync.AddDays(1);
        try
        {
            using var client = Utils.Utils.GetHttpClient(skipCertVerification: true);

            if (!string.IsNullOrEmpty(MyHome.Instance.Config.Password))
            {
                // generate token
                var token = JwtBuilder.Create()
                          .WithAlgorithm(new HMACSHA256Algorithm())
                          .WithSecret(MyHome.Instance.Config.Password)
                          .AddClaim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds())
                          .Encode();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(token);
            }

            // config
            var result = client.GetStringAsync($"{this.PeerServer}/api/config").Result;
            var json = JToken.Parse(result);
            json.SetObject(MyHome.Instance.Config);

            // rooms
            result = client.GetStringAsync($"{this.PeerServer}/api/rooms").Result;
            json = JArray.Parse(result);

            var receivedRooms = new List<string>();
            foreach (var item in json)
            {
                var room = MyHome.Instance.Rooms.FirstOrDefault(r => r.Name == (string)item["Name"]);
                if (room == null)
                {
                    logger.Info($"Add room '{item["Name"]}'");
                    room = new Room((string)item["Name"]);
                    MyHome.Instance.Rooms.Add(room);
                }
                item.SetObject(room);
                receivedRooms.Add(room.Name);

                var receivedDevices = new List<string>();
                foreach (var item2 in item["Devices"])
                {
                    var device = room.Devices.FirstOrDefault(d => d.Name == (string)item2["Name"]);
                    if (device == null)
                    {
                        logger.Info($"Add device '{item2["Name"]}' to room '{item["Name"]}'");
                        var type = System.Reflection.Assembly.GetExecutingAssembly().GetType((string)item2["$type"]);
                        if (type == null)
                        {
                            logger.Warn("No such device type: " + type);
                            continue;
                        }

                        device = (Device)Activator.CreateInstance(type, true);
                        device.Room = room;
                        device.Setup();
                        MyHome.Instance.DevicesSystem.Devices.Add(device);
                    }
                    item2.SetObject(device);
                    receivedDevices.Add(device.Name);
                }
                MyHome.Instance.DevicesSystem.Devices.RemoveAll(d => d.Room.Name == room.Name && !receivedDevices.Contains(d.Name));
            }
            MyHome.Instance.Rooms.RemoveAll(r => !receivedRooms.Contains(r.Name));
            // TODO: sync sensors data?

            // systems
            result = client.GetStringAsync($"{this.PeerServer}/api/systems").Result;
            json = JToken.Parse(result);
            // systems
            foreach (var item in json.OfType<JProperty>())
            {
                var system = MyHome.Instance.Systems.FirstOrDefault(kvp => kvp.Key.ToLower() == item.Name.ToLower()).Value;
                if (system == null)
                {
                    logger.Warn($"System '{item.Name}' not found");
                    continue;
                }

                item.Value.SetObject(system);
            }
            // actions
            var receivedActions = new List<string>();
            foreach (var item in json[nameof(ActionsSystem)]["Actions"])
            {
                var action = MyHome.Instance.ActionsSystem.Actions.Find(action => action.Name == (string)item["Name"]);
                if (action == null)
                {
                    logger.Info($"Add action: {item["Name"]}");
                    var actionType = System.Reflection.Assembly.GetExecutingAssembly().GetType((string)item["$type"]);
                    if (actionType == null)
                    {
                        logger.Warn("No such action type: " + actionType);
                        continue;
                    }

                    action = (BaseAction)Activator.CreateInstance(actionType, true);
                    action.Setup();
                    MyHome.Instance.ActionsSystem.Actions.Add(action);
                }
                item.SetObject(action);
                receivedActions.Add(action.Name);
            }
            MyHome.Instance.ActionsSystem.Actions.RemoveAll(a => !receivedActions.Contains(a.Name));

            MyHome.Instance.SystemChanged = true;
        }
        catch (Exception e)
        {
            logger.Warn("Failed to sync configuration from the main sever");
            logger.Debug(e);
        }
    }


    private static void AutoUpgradeBackupServer()
    {
        // try to upgrade the system if there is available
        if (MyHome.Instance.Notifications.Exists(n => n.Message() == MyHome.UpgradeNotification && n.Details() == "available"))
        {
            if (MyHome.Instance.Upgrade())
            {
                MyHome.Instance.Stop();
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => Environment.Exit(0));
            }
            else
            {
                MyHome.Instance.AddNotification("Cannot upgrade backup server")
                    .Validity(TimeSpan.FromDays(1))
                    .SendAlert(forceSend: true);
            }
        }
    }


}
