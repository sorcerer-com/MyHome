﻿namespace MyHome.Utils.Ewelink
{
    using System.Collections.Generic;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S125")]
    internal static class DeviceData
    {
        public static readonly Dictionary<string, int> DeviceChannelCount = new Dictionary<string, int>()
        {
          {"SOCKET", 1},
          {"SWITCH_CHANGE", 1},
          {"GSM_UNLIMIT_SOCKET", 1},
          {"SWITCH", 1},
          {"THERMOSTAT", 1},
          {"SOCKET_POWER", 1},
          {"GSM_SOCKET", 1},
          {"POWER_DETECTION_SOCKET", 1},
          {"SOCKET_2", 2},
          {"GSM_SOCKET_2", 2},
          {"SWITCH_2", 2},
          {"SOCKET_3", 3},
          {"GSM_SOCKET_3", 3},
          {"SWITCH_3", 3},
          {"SOCKET_4", 4},
          {"GSM_SOCKET_4", 4},
          {"SWITCH_4", 4},
          {"CUN_YOU_DOOR", 4}
        };

        public static readonly Dictionary<int, string> DeviceTypeUuid = new Dictionary<int, string>()
            {
          {1, "SOCKET" },
          {2, "SOCKET_2"},
          {3, "SOCKET_3"},
          {4, "SOCKET_4"},
          {6, "SWITCH"},
          {5, "SOCKET_POWER"},
          {7, "SWITCH_2"},
          {8, "SWITCH_3"},
          {9, "SWITCH_4"},
          {10, "OSPF"},
          {11, "CURTAIN"},
          {12, "EW-RE"},
          {13, "FIREPLACE"},
          {14, "SWITCH_CHANGE"},
          {15, "THERMOSTAT"},
          {16, "COLD_WARM_LED"},
          {17, "THREE_GEAR_FAN"},
          {18, "SENSORS_CENTER"},
          {19, "HUMIDIFIER"},
          {22, "RGB_BALL_LIGHT"},
          {23, "NEST_THERMOSTAT"},
          {24, "GSM_SOCKET"},
          {25, "AROMATHERAPY"},
          {26, "BJ_THERMOSTAT"},
          {27, "GSM_UNLIMIT_SOCKET"},
          {28, "RF_BRIDGE"},
          {29, "GSM_SOCKET_2"},
          {30, "GSM_SOCKET_3"},
          {31, "GSM_SOCKET_4"},
          {32, "POWER_DETECTION_SOCKET"},
          {33, "LIGHT_BELT"},
          {34, "FAN_LIGHT"},
          {35, "EZVIZ_CAMERA"},
          {36, "SINGLE_CHANNEL_DIMMER_SWITCH"},
          {38, "HOME_KIT_BRIDGE"},
          {40, "FUJIN_OPS"},
          {41, "CUN_YOU_DOOR"},
          {42, "SMART_BEDSIDE_AND_NEW_RGB_BALL_LIGHT"},
          {43, ""},
          {44, ""},
          {45, "DOWN_CEILING_LIGHT"},
          {46, "AIR_CLEANER"},
          {49, "MACHINE_BED"},
          {51, "COLD_WARM_DESK_LIGHT"},
          {52, "DOUBLE_COLOR_DEMO_LIGHT"},
          {53, "ELECTRIC_FAN_WITH_LAMP"},
          {55, "SWEEPING_ROBOT"},
          {56, "RGB_BALL_LIGHT_4"},
          {57, "MONOCHROMATIC_BALL_LIGHT"},
          {59, "MEARICAMERA"},
          {1001, "BLADELESS_FAN"},
          {1002, "NEW_HUMIDIFIER"},
          {1003, "WARM_AIR_BLOWER"}
        };


        /*private static Dictionary<string, int>? deviceChannelCount;

        private static Dictionary<int, string>? deviceTypeUuid;

        public static Dictionary<string, int> DeviceChannelCount
        {
            get
            {
                if (deviceChannelCount == null)
                {
                    LoadData();
                }

                return deviceChannelCount;
            }
        }

        public static Dictionary<int, string> DeviceTypeUuid
        {
            get
            {
                if (deviceTypeUuid == null)
                {
                    LoadData();
                }

                return deviceTypeUuid;
            }
        }

        private static void LoadData()
        {
            deviceChannelCount = LoadAndDeserializeResource<Dictionary<string, int>>("devices-channel-length.json");

            deviceTypeUuid = LoadAndDeserializeResource<Dictionary<int, string>>("devices-type-uuid.json");
        }

        private static T LoadAndDeserializeResource<T>(string resourceName)
        {
            var assembly = typeof(DeviceData).GetTypeInfo().Assembly;
            var serializer = new JsonSerializer();
            using var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Resources.{resourceName}");
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);
            return serializer.Deserialize<T>(jsonReader);
        }*/
    }
}