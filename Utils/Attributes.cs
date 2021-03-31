using System;

namespace MyHome.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UiProperty : Attribute
    {
        public bool Setting { get; }

        public UiProperty(bool setting = false)
        {
            this.Setting = setting;
        }
    }
}
