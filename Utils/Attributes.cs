using System;

namespace MyHome.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UiPropertyAttribute : Attribute
    {
        public bool Setting { get; }

        public string Hint { get; }

        public UiPropertyAttribute(bool setting = false) : this(setting, "")
        {
        }

        public UiPropertyAttribute(bool setting, string hint)
        {
            this.Setting = setting;
            this.Hint = hint;
        }
    }
}
