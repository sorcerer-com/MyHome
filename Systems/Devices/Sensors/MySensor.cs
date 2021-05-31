using MyHome.Models;
using MyHome.Utils;

using Newtonsoft.Json.Linq;

namespace MyHome.Systems.Devices.Sensors
{
    public class MySensor : BaseSensor
    {
        private MySensor() : this(null, null, null, null) { } // for json deserialization

        public MySensor(DevicesSystem owner, string name, Room room, string address) : base(owner, name, room, address)
        {
        }

        protected override JToken ReadDataInternal()
        {
            return Services.GetJsonContent(this.Address);
        }
    }
}
