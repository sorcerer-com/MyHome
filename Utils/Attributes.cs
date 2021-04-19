using System;

namespace MyHome.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UiProperty : Attribute
    {
        public bool Setting { get; }

        public string Hint { get; }

        public UiProperty(bool setting = false) : this(setting, "")
        {
        }

        public UiProperty(bool setting, string hint)
        {
            this.Setting = setting;
            this.Hint = hint;
        }
    }
}
