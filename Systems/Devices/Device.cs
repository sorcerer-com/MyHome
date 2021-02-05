namespace MyHome.Systems.Devices
{
    public abstract class Device
    {
        public DevicesSystem Owner { get; }

        public string Name { get; }

        public string Room { get; set; } // TODO: should be in the list of rooms?


        public Device(DevicesSystem owner, string name, string room)
        {
            this.Owner = owner;
            this.Name = name;
            this.Room = room;
        }

        public abstract void Update();
    }
}
