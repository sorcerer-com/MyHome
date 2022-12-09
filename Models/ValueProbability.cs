using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using NLog;

namespace MyHome.Models
{
    public class ValueProbability
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly int minSamples;
        private readonly double cleanupThreshold;
        [JsonProperty]
        private readonly Dictionary<string, (int Samples, int Count)> data; // value / (samples / count)

        public ValueProbability(int minSamples = 10, double cleanupThreshold = 0.1)
        {
            this.minSamples = minSamples;
            this.cleanupThreshold = cleanupThreshold;
            this.data = new Dictionary<string, (int samples, int count)>();
        }

        public void AddValue(string value, int sample = 1, bool maxCountInit = false)
        {
            this.Cleanup(maxCountInit);
            if (!this.data.ContainsKey(value))
            {
                if (sample == 0)
                    return;

                int count = maxCountInit && this.data.Count > 0 ? this.data.Values.Max(d => d.Count) : 0;
                this.data[value] = (0, count);
            }

            this.data[value] = (this.data[value].Samples + sample, this.data[value].Count + 1);
            logger.Debug($"Add value: {value}, sample: {sample}, count: {this.data[value].Count}");
        }

        public double GetProbability(string value)
        {
            if (!this.data.ContainsKey(value))
            {
                logger.Debug($"No statistic for this value: {value}");
                return 0;
            }
            // if there is not enough statistic data
            if (this.data[value].Count < this.minSamples)
            {
                logger.Debug($"Cannot get probability, count: {this.data[value].Count}, minSamples: {this.minSamples}");
                return -1;
            }

            var prob = (double)this.data[value].Samples / this.data[value].Count;
            logger.Debug($"Value: {value}, probability: {prob}");
            return prob;
        }

        private void Cleanup(bool thresholdOfMaxCount)
        {
            var toRemove = new List<string>();
            int maxCount = thresholdOfMaxCount && this.data.Count > 0 ? this.data.Values.Max(d => d.Count) : 1;
            foreach (var kvp in this.data)
            {
                var prob = (double)kvp.Value.Samples / kvp.Value.Count;
                if (prob < this.cleanupThreshold / maxCount)
                {
                    logger.Debug($"Removing value: {kvp.Key}, prob: {prob}, threshold: {this.cleanupThreshold}");
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var value in toRemove)
                this.data.Remove(value);
        }
    }
}
