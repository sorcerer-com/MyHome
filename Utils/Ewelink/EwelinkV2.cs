using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyHome.Utils.Ewelink;


[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S3928")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S112")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S1117")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S3260")]
public class EwelinkV2
{
    //https://coolkit-technologies.github.io/eWeLink-API/#/en/APICenterV2?id=get-device-or-group-status

    // can be created in https://dev.ewelink.cc/#/ if this stop working
    // from https://github.com/AlexxIT/SonoffLAN/blob/c317407a09e5c5e9aaf2a95e044e5afb5a367dec/custom_components/sonoff/core/ewelink/cloud.py#L43
    private static readonly Dictionary<string, string> Apps = new()
    {
        { "4s1FXKC9FaGfoqXhmXSJneb3qcm1gOak", "oKvCM06gvwkRbfetd6qWRrbC3rFrbIpV" },
        { "YzfeftUVcZ6twZw1OoVKPRFYTrGEg01Q", "4G91qSoboqYO4Y0XJ0LPPKIsq8reHdfa" },
        { "oeVkj2lYFGnJu5XUtWisfW4utiN4u9Mq", "6Nz4n0xA8s8qdxQf2GqurZj2Fs55FUvM" },
        { "R8Oq3y0eSZSYdKccHlrQzT1ACCOUT9Gv", "1ve5Qk9GXfUhKAn1svnKwpAlxXkMarru" }
    };

    private string AppId = "4s1FXKC9FaGfoqXhmXSJneb3qcm1gOak";

    private string AppSecret = "oKvCM06gvwkRbfetd6qWRrbC3rFrbIpV";

    private readonly HttpClient HttpClient = new();

    private readonly Dictionary<string, EwelinkDevice> devicesCache = new();

    private string region = "eu";

    private readonly string countryCode;

    private readonly string email;

    private readonly string phoneNumber;

    private readonly string password;

    private string at;

    private string apiKey;

    public EwelinkV2(
        string countryCode = null,
        string email = null,
        string password = null,
        string phoneNumber = null,
        string region = "eu",
        string at = null)
    {
        var check = CheckLoginParameters(countryCode, email, phoneNumber, password, at);

        if (!check)
            throw new Exception("invalidCredentials");

        this.region = region;
        this.countryCode = countryCode;
        this.phoneNumber = phoneNumber;
        this.email = email;
        this.password = password;
        this.at = at;
        this.apiKey = null;

        this.HttpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public Uri ApiUri => new($"https://{this.region}-apia.coolkit.cc/v2");
    public Uri ApiWebSocketUri => new($"wss://{this.region}-pconnect3.coolkit.cc:8080/api/ws");

    public async Task<SensorData> GetDeviceCurrentSensorData(string deviceId)
    {
        var device = await this.GetDevice(deviceId, true);

        var parameters = device.Paramaters;
        if (!parameters.CurrentTemperature.HasValue && !parameters.CurrentHumidity.HasValue)
            return null;

        if (parameters.CurrentTemperature.HasValue)
            return new SensorData(deviceId, SensorType.Temperature, parameters.CurrentTemperature.Value);

        if (parameters.CurrentHumidity.HasValue)
            return new SensorData(deviceId, SensorType.Humidity, parameters.CurrentHumidity.Value);

        return null;
    }

    public async Task<(string Email, string Region)> GetRegion()
    {
        if (string.IsNullOrWhiteSpace(this.email))
            throw new ArgumentNullException(nameof(this.email));

        if (string.IsNullOrWhiteSpace(this.password))
            throw new ArgumentNullException(nameof(this.password));

        var credentials = await this.GetCredentials();

        return (credentials.User?.Email, credentials.Region);
    }

    public Task<EwelinkDevice> GetDevice(string deviceId)
    {
        return this.GetDevice(deviceId, false);
    }

    public int GetDeviceChannelCountByUuid(int uuid)
    {
        var deviceType = GetDeviceTypeByUiid(uuid);
        return DeviceData.DeviceChannelCount[deviceType];
    }

    public Task ToggleDevice(string deviceId, int channel = 1)
    {
        return this.SetDevicePowerState(deviceId, "toggle", channel);
    }

    public async Task<int> GetDeviceChannelCount(string deviceId)
    {
        var device = await this.GetDevice(deviceId);
        var uiid = device.Extra.Extended.Uiid;
        var switchesAmount = this.GetDeviceChannelCountByUuid(uiid);

        return switchesAmount;
    }

    public async Task SetDevicePowerState(string deviceId, string state, int channel = 1)
    {
        var device = await this.GetDevice(deviceId);
        var uiid = device.Extra.Extended.Uiid;

        var status = device.Paramaters.Switch;
        var switches = device.Paramaters.Switches;

        var switchesAmount = this.GetDeviceChannelCountByUuid(uiid);

        if (switchesAmount > 0 && switchesAmount < channel)
            throw new Exception(NiceError.Custom[CustomErrors.Ch404]);

        var stateToSwitch = state;
        dynamic parameters = new System.Dynamic.ExpandoObject();

        if (switches != null)
            status = switches[channel - 1].Switch;

        if (state == "toggle")
            stateToSwitch = status == SwitchState.On ? "off" : "on";

        if (switches != null)
        {
            parameters.switches = switches;
            parameters.switches[channel - 1].@switch = stateToSwitch;
        }
        else
        {
            parameters.@switch = stateToSwitch;
        }

        dynamic response = await this.MakeRequest(
                               "/device/thing/status",
                               body: new
                               {
                                   type = 1,
                                   id = deviceId,
                                   @params = parameters,
                               },
                               method: HttpMethod.Post);

        int? responseError = response.error;

        if (responseError > 0)
            throw new Exception(NiceError.Errors[responseError.Value]);
    }

    public async Task TransmitRfChannel(string deviceId, int channel = 0)
    {
        dynamic parameters = new System.Dynamic.ExpandoObject();
        (parameters as IDictionary<string, object>)?.Add($"rfChl", channel);
        parameters.cmd = "transmit";

        dynamic response = await this.MakeRequest(
                               "/device/thing/status",
                               body: new
                               {
                                   type = 1,
                                   id = deviceId,
                                   @params = parameters,
                               },
                               method: HttpMethod.Post);

        int? responseError = response.error;

        if (responseError > 0)
            throw new Exception(NiceError.Errors[responseError.Value]);
    }

    public async Task TransmitRfChannelByWebSocket(string deviceId, int channel = 0, CancellationToken? cancellationToken = null)
    {
        var device = await this.GetDevice(deviceId);

        var payload = new
        {
            action = "update",
            apikey = device.ApiKey,
            selfApikey = this.apiKey,
            deviceid = deviceId,
            @params = new
            {
                cmd = "transmit",
                rfChl = channel
            },
            userAgent = "app",
            sequence = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond,
        };
        await MakeWebSocketRequest(payload, cancellationToken);
    }

    public Task<ClientWebSocket> OpenWebSocket()
    {
        return this.OpenWebSocket(CancellationToken.None);
    }

    public async Task<ClientWebSocket> OpenWebSocket(CancellationToken cancellationToken)
    {
        var wssLoginPayload = JsonConvert.SerializeObject(new
        {
            action = "userOnline",
            this.at,
            apikey = this.apiKey,
            appid = AppId,
            nonce = Utilities.Nonce,
            ts = Utilities.Timestamp,
            userAgent = "ewelink-api",
            sequence = 1,
            version = 8,
        });

        var wsp = new ClientWebSocket();
        wsp.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(120000);

        await wsp.ConnectAsync(this.ApiWebSocketUri, CancellationToken.None);

        var data = new ArraySegment<byte>(Encoding.UTF8.GetBytes(wssLoginPayload));
        await wsp.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);

        var buffer = new ArraySegment<byte>(new byte[8192]);
        var result = await wsp.ReceiveAsync(buffer, cancellationToken);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var text = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
            var json = JsonConvert.DeserializeObject<dynamic>(text);
            int? error = json.error;
            if (error > 0)
            {
                throw new Exception($"Error: {error}");
            }
        }

        return wsp;
    }

    public async Task<List<EwelinkDevice>> GetDevices()
    {
        dynamic response = await this.MakeRequest(
                               "/device/thing",
                               query: new
                               {
                                   lang = "en",
                                   num = 0
                               });

        JToken jtoken = response.thingList;
        if (jtoken == null)
            throw new HttpRequestException(NiceError.Custom[CustomErrors.NoDevices]);

        var devicelist = jtoken.Where(i => (int)i["itemType"] == 1 || (int)i["itemType"] == 2).Select(i => i["itemData"].ToObject<EwelinkDevice>()).ToList();
        foreach (var device in devicelist)
        {
            if (!this.devicesCache.ContainsKey(device.Deviceid))
                this.devicesCache.Add(device.Deviceid, device);
            else
            {
                this.devicesCache[device.Deviceid] = device;
            }
        }

        return devicelist;
    }

    public async Task<string> GetFirmwareVersion(string deviceId)
    {
        var device = await this.GetDevice(deviceId);
        return device.Paramaters.FirmWareVersion;
    }

    public async Task<Credentials> GetCredentials()
    {
        var body = CredentialsPayload(countryCode: this.countryCode, email: this.email, phoneNumber: this.phoneNumber, password: this.password);

        var uri = new Uri($"{this.ApiUri}/user/login");
        var httpMessage = new HttpRequestMessage(HttpMethod.Post, uri);
        httpMessage.Headers.Add("X-CK-Appid", this.AppId);
        httpMessage.Headers.Add("X-CK-Nonce", Utilities.NonceV2);
        httpMessage.Headers.Add("Authorization", $"Sign {MakeAuthorizationSign(body, this.AppSecret)}");
        httpMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

        var response = await this.HttpClient.SendAsync(httpMessage);

        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();
        dynamic json = JsonConvert.DeserializeObject(jsonString);

        int? errorValue = json.error;
        string region = json?.data?.region;

        if (errorValue.HasValue && new[] { 400, 401, 404 }.Contains(errorValue.Value))
            throw new HttpRequestException(NiceError.Errors[406]);

        // different region
        if (errorValue == 10004 && region != null)
        {
            if (this.region != region)
            {
                this.region = region;
                return await this.GetCredentials();
            }

            throw new ArgumentOutOfRangeException(nameof(this.region), "Region does not exist");
        }

        // try different app id
        if (errorValue == 407)
        {
            var appIds = Apps.Keys.ToList();
            if (appIds.IndexOf(this.AppId) < appIds.Count - 1)
            {
                this.AppId = appIds[appIds.IndexOf(this.AppId) + 1];
                this.AppSecret = Apps[this.AppId];
                return await this.GetCredentials();
            }
        }

        if (errorValue.HasValue && errorValue != 0)
            throw new HttpRequestException(jsonString);

        JToken token = json.data;
        var credentials = token.ToObject<Credentials>();
        this.apiKey = credentials.User.Apikey;
        this.at = credentials.At;
        return credentials;
    }

    private async Task<dynamic> MakeRequest(string path, Uri baseUri = null, object body = null, object query = null, HttpMethod method = null, bool retry = false)
    {
        if (method == null)
            method = HttpMethod.Get;

        if (string.IsNullOrWhiteSpace(this.at))
            await this.GetCredentials();

        if (baseUri == null)
            baseUri = this.ApiUri;

        var uriBuilder = new UriBuilder($"{baseUri}{path}")
        {
            Query = query != null ? ToQueryString(query) : string.Empty,
        };

        var uri = uriBuilder.Uri;
        var httpMessage = new HttpRequestMessage(method, uri);
        httpMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.at);

        if (body != null)
        {
            var data = JsonConvert.SerializeObject(body);
            httpMessage.Content = new StringContent(data, Encoding.UTF8, "application/json");
        }

        var response = await this.HttpClient.SendAsync(httpMessage);

        response.EnsureSuccessStatusCode();
        var jsonString = await response.Content.ReadAsStringAsync();
        dynamic json = JsonConvert.DeserializeObject(jsonString);

        int? error = json.error;

        if (error > 0)
        {
            if (error.Value == 401 && !retry) // retry with get credentials
            {
                this.at = null;
                return await this.MakeRequest(path, baseUri, body, query, method, true);
            }
            else
                throw new HttpRequestException(NiceError.Errors[error.Value]);
        }

        return json.data;
    }

    private async Task MakeWebSocketRequest(object payload, CancellationToken? cancellationToken)
    {
        var data = JsonConvert.SerializeObject(payload);
        var encoded = Encoding.UTF8.GetBytes(data);
        var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length);

        var ws = await this.OpenWebSocket(cancellationToken ?? CancellationToken.None);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken ?? CancellationToken.None);

        var jsonString = await ReadFromWebSocket(ws);
        dynamic json = JsonConvert.DeserializeObject(jsonString);

        int? error = json.error;

        if (error > 0)
            throw new HttpRequestException(NiceError.Errors[error.Value]);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken ?? CancellationToken.None);
    }

    private async Task<EwelinkDevice> GetDevice(string deviceId, bool noCacheLoad)
    {
        if (noCacheLoad)
            this.devicesCache.Clear();

        await this.GetDevices();
        if (this.devicesCache.TryGetValue(deviceId, out var device))
            return device;
        return null;
    }

    private static bool CheckLoginParameters(
        string countryCode,
        string email,
        string phoneNumber,
        string password,
        string at)
    {
        if (email != null && phoneNumber != null)
            return false;

        if ((email != null && password != null) || (countryCode != null && phoneNumber != null && password != null) || at != null)
            return true;

        return false;
    }

    private static string ToQueryString(object qs)
    {
        var properties = from p in qs.GetType().GetProperties()
                         where p.GetValue(qs, null) != null
                         select p.Name + "=" + System.Web.HttpUtility.UrlEncode(p.GetValue(qs, null).ToString());

        return string.Join("&", properties.ToArray());
    }

    private static string MakeAuthorizationSign(PayLoad body, string appSecret)
    {
        var crypto = new HMACSHA256 { Key = Encoding.UTF8.GetBytes(appSecret) };
        var hash = crypto.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
        return Convert.ToBase64String(hash);
    }

    private static PayLoad CredentialsPayload(string countryCode, string email, string phoneNumber, string password)
    {
        return new PayLoad(
            countryCode,
            email,
            phoneNumber,
            password);
    }

    private static string GetDeviceTypeByUiid(int uiid)
    {
        if (DeviceData.DeviceTypeUuid.TryGetValue(uiid, out var type))
            return type;

        return string.Empty;
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    private class PayLoad
    {
        public PayLoad(string countryCode, string email, string phoneNumber, string password)
        {
            this.CountryCode = countryCode;
            this.Email = email;
            this.PhoneNumber = phoneNumber;
            this.Password = password;
        }

        [JsonProperty("countryCode")]
        public string CountryCode { get; }

        [JsonProperty("email")]
        public string Email { get; }

        [JsonProperty("phoneNumber")]
        public string PhoneNumber { get; }

        [JsonProperty("password")]
        public string Password { get; }
    }

    private static async Task<string> ReadFromWebSocket(ClientWebSocket ws)
    {
        ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[8192]);
        using var ms = new MemoryStream();

        WebSocketReceiveResult result = null;
        do
        {
            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            ms.Write(buffer.Array, buffer.Offset, result.Count);
        }
        while (!result.EndOfMessage);

        ms.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(ms, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
