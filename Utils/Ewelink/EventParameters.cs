namespace MyHome.Utils.Ewelink
{
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public class EventParameters
    {
        public SwitchState? Switch { get; set; }

        public LinkSwitch?[] Switches { get; set; }
    }
}