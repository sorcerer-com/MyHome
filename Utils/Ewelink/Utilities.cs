using System;
using System.Text.RegularExpressions;

namespace MyHome.Utils.Ewelink
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S125")]
    public static class Utilities
    {
        public static readonly Random Random = new();
        public static readonly Regex Regex = new("[^a-zA-Z0-9]");


        public static long Timestamp => (long)Math.Floor(DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000.0);

        //public static string Nonce => long.Parse(Random.NextDouble().ToString().Substring(2)).ToBase36().Substring(5);
        public static string Nonce => Convert.ToBase64String(BitConverter.GetBytes(long.Parse(Random.NextDouble().ToString()[2..])))[5..];

        public static string NonceV2 => Regex.Replace(Convert.ToBase64String(BitConverter.GetBytes(Random.NextInt64())), "")[..8];
    }
}