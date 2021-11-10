using MyHome.Utils;

using Newtonsoft.Json.Linq;

namespace MyHome.Systems.Devices.Sensors
{
    public class MySensor : BaseSensor
    {
        public MySensor()
        {
        }


        protected override JToken ReadDataInternal()
        {
            return Services.GetJsonContent(this.Address);
        }
    }
}
