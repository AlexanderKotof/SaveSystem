using System;

namespace SaveSystem.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveDataAttribute : Attribute { }
}
