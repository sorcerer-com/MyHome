using MyHome.Models;

namespace MyHome.Systems.Devices
{
    public abstract class Device
    {
        public DevicesSystem Owner { get; }

        public string Name { get; }

        public Room Room { get; set; }


        public Device(DevicesSystem owner, string name, Room room)
        {
            this.Owner = owner;
            this.Name = name;
            this.Room = room;
        }

        public abstract void Update();
    }
}
