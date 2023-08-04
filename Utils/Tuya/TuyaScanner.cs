using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

namespace MyHome.Utils.Tuya;

/// <summary>
/// Scanner to discover devices over local network.
/// </summary>
public class TuyaScanner
{
    private const ushort UDP_PORT31 = 6666;      // Tuya 3.1 UDP Port
    private const ushort UDP_PORT33 = 6667;      // Tuya 3.3 encrypted UDP Port
    private const string UDP_KEY = "yGAdlopoPVldABfn";

    private bool running = false;
    private UdpClient udpServer31 = null;
    private UdpClient udpServer33 = null;
    private readonly List<TuyaDeviceScanInfo> devices = new();

    /// <summary>
    /// Even that will be called on every broadcast message from devices.
    /// </summary>
    public event EventHandler<TuyaDeviceScanInfo> OnDeviceInfoReceived;
    /// <summary>
    /// Even that will be called only once for every device.
    /// </summary>
    public event EventHandler<TuyaDeviceScanInfo> OnNewDeviceInfoReceived;

    /// <summary>
    /// Starts scanner.
    /// </summary>
    public void Start()
    {
        this.Stop();
        this.running = true;
        this.devices.Clear();

        this.udpServer31 = new UdpClient(UDP_PORT31);
        this.udpServer33 = new UdpClient(UDP_PORT33);

        Thread listener = new Thread(this.UdpListener);
        listener.Start();
    }

    /// <summary>
    /// Stops scanner.
    /// </summary>
    public void Stop()
    {
        this.running = false;
        if (this.udpServer31 != null)
        {
            this.udpServer31.Dispose();
            this.udpServer31 = null;
        }
        if (this.udpServer33 != null)
        {
            this.udpServer33.Dispose();
            this.udpServer33 = null;
        }
    }

    private void UdpListener(object o)
    {
        byte[] udp_key;
        using (var md5 = MD5.Create())
        {
            udp_key = md5.ComputeHash(Encoding.ASCII.GetBytes(UDP_KEY));
        }

        while (this.running)
        {
            try
            {
                IPEndPoint ep = null;
                if (this.udpServer31.Available > 20)
                {
                    var data = this.udpServer31.Receive(ref ep);
                    var response = TuyaParser.DecodeResponse(data, udp_key, TuyaProtocolVersion.V31);
                    this.Parse(response.JSON);
                }
                if (this.udpServer33.Available > 20)
                {
                    var data = this.udpServer33.Receive(ref ep);
                    var response = TuyaParser.DecodeResponse(data, udp_key, TuyaProtocolVersion.V33);
                    this.Parse(response.JSON);
                }
            }
            catch
            {
                if (!this.running) return;
                throw;
            }
        }
    }

    private void Parse(string json)
    {
        var deviceInfo = JsonConvert.DeserializeObject<TuyaDeviceScanInfo>(json);
        OnDeviceInfoReceived?.Invoke(this, deviceInfo);
        if ((OnNewDeviceInfoReceived) != null && !this.devices.Contains(deviceInfo))
        {
            this.devices.Add(deviceInfo);
            OnNewDeviceInfoReceived?.Invoke(this, deviceInfo);
        }
    }
}