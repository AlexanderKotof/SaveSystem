using System;

namespace SaveSystem.Attributes
{
    /// <summary>
    /// Attribute marks class, property or field for SaveSystem.SourceGenerator to automatically generate a dto and methods to easily convert to each other.
    /// See: ...
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SaveDataAttribute : Attribute
    {
        /// <summary>
        /// Allows automatically filter collections. Appends Where(...) to the ToSaveData() generated method for this property.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Allows serialize not the whole object, but only the property of it. Requires manual resolve. Example: ScriptableConfig.Name or Unit.Id
        /// </summary>
        public string Select { get; set; }
    }
}
