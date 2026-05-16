using System;
using System.Collections.Generic;
using SaveSystem.Attributes;
using UniRx;

namespace SaveSystem.Example
{
    [SaveData]
    public class GameDataModel
    {

        // In
        [SaveData]
        public SomeDataModel SomeDataModel { get; } = new();

        // ...
    }

    [Serializable]
    public struct SerializableData
    {
        public int Value;
        public string Name;
    }

    /// <summary>
    /// Example of some in-game reactive data model
    /// </summary>
    [SaveData]
    public class SomeDataModel
    {
        // ...

        // Any serializable classes/structs
        [SaveData]
        public SerializableData SerializableData { get; set; } = new();

        // Reactive properties supported
        [SaveData]
        public ReactiveProperty<int> Health { get; } = new ();

        // Reactive collections turns in arrays
        // Don't serialize ScriptableObject but just it Id
        [SaveData(Select = "Id")]
        public ReactiveCollection<GameConfigBase> Configs { get; } = new ();

        // Read-only properties serialization
        // IEnumerable turns into array
        [SaveData]
        public IEnumerable<string> SomeSavedStrings => new [] { "abc", "def", "ghi", "jkl" };

        // Filter collections before save
        [SaveData(Filter = "x => x.someData == 42")]
        public IEnumerable<GameConfigBase> SomeSavedStringsWithFilter => Configs;

        // ...
    }
}
