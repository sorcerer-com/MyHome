using System;
using System.Collections.Generic;
using System.IO;

using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json;

namespace MyHome.Systems
{
    public class PredictionSystem : BaseSystem
    {
        public static readonly string DataFilePath = Path.Join(Config.BinPath, "probabilities.json");

        public enum ProbabilityType
        {
            NoPresence
        }

        private readonly Dictionary<ProbabilityType, ValueProbability> probabilities;
        private int hourUpdate;

        public PredictionSystem()
        {
            this.probabilities = new Dictionary<ProbabilityType, ValueProbability>
            {
                [ProbabilityType.NoPresence] = new ValueProbability()
            };

            this.hourUpdate = DateTime.Now.Hour;
        }

        public override void Setup()
        {
            base.Setup();
            if (File.Exists(DataFilePath))
            {
                var json = File.ReadAllText(DataFilePath);
                JsonConvert.PopulateObject(json, this.probabilities);
            }

            MyHome.Instance.Events.Handler += this.Events_Handler;
        }

        private void Events_Handler(object sender, GlobalEventArgs e)
        {
            if (e.EventType == GlobalEventTypes.PresenceChanged)
            {
                var now = DateTime.Now;
                this.probabilities[ProbabilityType.NoPresence].AddValue($"{(int)now.DayOfWeek}{now.Hour}", (bool)e.Data ? 0 : 1, true);
                // save
                var json = JsonConvert.SerializeObject(this.probabilities, Formatting.Indented);
                File.WriteAllText(DataFilePath, json);
            }
        }

        protected override void Update()
        {
            base.Update();

            var now = DateTime.Now;
            if (now.Hour != this.hourUpdate)
            {
                this.hourUpdate = now.Hour;

                var presence = MyHome.Instance.SecuritySystem.Present.Count > 0;
                this.probabilities[ProbabilityType.NoPresence].AddValue($"{(int)now.DayOfWeek}{now.Hour}", presence ? 0 : 1, true);
                // save
                var json = JsonConvert.SerializeObject(this.probabilities, Formatting.Indented);
                File.WriteAllText(DataFilePath, json);
            }
        }
    }
}
