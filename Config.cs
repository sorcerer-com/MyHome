﻿using MyHome.Utils;
using System.Collections.Generic;
using System.IO;

namespace MyHome
{
    public class Config
    {
        public static readonly string DataFilePath = Path.Join("bin", "data.json");

        public void Load(Dictionary<string, object> data)
        {
            this.SetJsonValues(data);
        }

        public Dictionary<string, object> Save()
        {
            return this.GetJsonValues();
        }
    }
}