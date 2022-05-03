using System;

namespace MyHome.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UiPropertyAttribute : Attribute
    {
        public bool Setting { get; }

        public string Hint { get; }

        public string Selector { get; }

        public bool Code { get; }


        public UiPropertyAttribute(bool setting = false, string hint = "", string selector = null, bool code = false)
        {
            this.Setting = setting;
            this.Hint = hint;
            this.Selector = selector;
            this.Code = code;
        }
    }
}
