using MyHome.Models;

namespace MyHome.Systems.Devices
{
    public abstract class Device
    {
        public DevicesSystem Owner { get; set; }

        public string Name { get; set; }

        public Room Room { get; set; }


        private Device() : this(null, null, null) { } // for json deserialization

        public Device(DevicesSystem owner, string name, Room room)
        {
            this.Owner = owner;
            this.Name = name;
            this.Room = room;
        }

        public virtual void Update()
        {
        }
    }
}
