﻿using System.IO;

using MyHome.Utils;

namespace MyHome.Models
{
    public class Config
    {
        public static readonly string BinPath = "bin";
        public static readonly string DataFilePath = Path.Join(BinPath, "data.json");

        public static readonly string SoundsPath = Path.Combine("External", "Sounds");


        [UiProperty(true)]
        public bool SavePrettyJson { get; set; } = false;

        [UiProperty(true, "sha256 hash")]
        public string Password { get; set; } = "";

        [UiProperty(true, "start - end")]
        public (int start, int end) QuietHours { get; set; } = (0, 0);

        [UiProperty(true, "(Latitude, Longitude)")]
        public (double lat, double @long) Location { get; set; } = (0.0, 0.0);


        [UiProperty(true, "08xxxxxxxx")]
        public string GsmNumber { get; set; } = "";

        [UiProperty(true, "base64 encode")]
        public string MyTelenorPassword { get; set; } = "";

        [UiProperty(true, "host[:port]")]
        public string SmtpServerAddress { get; set; } = "";

        [UiProperty(true)]
        public string Email { get; set; } = "";

        [UiProperty(true, "base64 encode")]
        public string EmailPassword { get; set; } = "";


        [UiProperty(true, "host[:port]")]
        public string MqttServerAddress { get; set; } = "";

        [UiProperty(true)]
        public string MqttUsername { get; set; } = "";

        [UiProperty(true, "base64 encode")]
        public string MqttPassword { get; set; } = "";

        [UiProperty(true)]
        public string EwelinkEmail { get; set; } = "";

        [UiProperty(true, "base64 encode")]
        public string EwelinkPassword { get; set; } = "";

        [UiProperty(true)]
        public string ImagesPath { get; set; } = Path.Combine(BinPath, "Images");

        [UiProperty(true)]
        public string SongsPath { get; set; } = Path.Combine(BinPath, "Songs");
    }
}
