using Newtonsoft.Json.Linq;

namespace MyHome.Systems.Devices
{
    public class MyMultiSensor : BaseSensor
    {
        public MyMultiSensor(DevicesSystem owner, string name, string room, string address) : base(owner, name, room, address)
        {
        }

        protected override JToken ReadDataInternal()
        {
            return Services.GetJsonContent(this.Address);
        }
    }
}
