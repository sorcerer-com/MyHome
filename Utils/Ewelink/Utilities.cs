namespace MyHome.Utils.Ewelink
{
    using System;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "S125")]
    public static class Utilities
    {
        public static readonly Random Random = new Random();


        public static long Timestamp => (long)Math.Floor(DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000.0);

        //public static string Nonce => long.Parse(Random.NextDouble().ToString().Substring(2)).ToBase36().Substring(5);
        public static string Nonce => Convert.ToBase64String(BitConverter.GetBytes(long.Parse(Random.NextDouble().ToString().Substring(2)))).Substring(5);
    }
}