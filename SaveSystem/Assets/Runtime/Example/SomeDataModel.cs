using System;
using System.Collections.Generic;
using System.Linq;
using SaveSystem.Attributes;
using UniRx;

namespace SaveSystem.Example
{
    /// <summary>
    /// Example of some in-game models aggregate
    /// </summary>
    [SaveData]
    public class GameDataModelAggregate
    {
        // Nested object with SaveData attribute
        [SaveData]
        public SomeDataModel SomeDataModel { get; } = new();

        [SaveData]
        public ReactiveProperty<AnotherModel> ModelInReactiveProperty { get; } = new();

        public float a;
        // ...
    }

    // Serializable objects leaved as is
    [Serializable]
    public struct SerializableData
    {
        public int Value;
        public string Name;
    }

    [SaveData]
    public class AnotherModel
    {
        [SaveData]
        public SerializableData SerializableData { get; set; } = new();
    }

    /// <summary>
    /// Example of some in-game reactive data model
    /// </summary>
    [SaveData]
    public class SomeDataModel
    {
        // ...

        // Any serializable values and objects
        [SaveData]
        public SerializableData SerializableData { get; set; } = new();
        [SaveData]
        public SerializableData SerializableDataGetOnly { get; } = new();

        // Reactive properties supported
        [SaveData]
        public ReactiveProperty<int> Health { get; } = new();

        // All collection types turns into arrays
        // Select: don't serialize ScriptableObject but just it's Id
        [SaveData(Select = "Id")]
        public ReactiveCollection<GameConfigBase> Configs { get; } = new ();

        // Get-only properties serialization
        // IEnumerable turns into array
        [SaveData]
        public IEnumerable<string> SomeSavedStrings => new [] { "abc", "def", "ghi", "jkl" };

        // Filter collections before save
        [SaveData(Filter = "x => x.someData == 42", Select = "Id")]
        public IEnumerable<GameConfigBase> ConfigsIdWithFilter => Configs;

        // Filter collection and select property for save
        [SaveData(Filter = "x => x.someData == 42", Select = "name")]
        public IEnumerable<GameConfigBase> ConfigsWithSelector => Configs;

        // In class marked with SaveData properties without SaveData attribute will not be saved
        // as it is not serialized as itself
        public float SomeFloat = 1.0f;

        // Fields are not supported right now
        // [SaveData]
        public int SomeIntValue = 69;

        // ...
    }
}
