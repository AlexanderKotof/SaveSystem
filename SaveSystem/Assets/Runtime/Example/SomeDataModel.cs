using SaveSystem.Attributes;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;

namespace SaveSystem.Example
{

    [SaveData]
    public class GameDataModel
    {
        [SaveData]
        public SomeDataModel SomeDataModel { get; }

        // ...
    }

    [SaveData]
    public class SomeDataModel
    {
        [SaveData]
        public ReactiveProperty<int> Health { get; } = new ();

        [SaveData]
        public ReactiveCollection<GameConfigBase> Configs { get; } = new ReactiveCollection<GameConfigBase>();

        // ...
    }

    public class SaveSystemExample
    {
        public void Save(GameDataModel model)
        {
            var dto = model.ToSaveData();

            // serialize it as you want

            // write to file, send to server, etc.

            Debug.Log($"Data written:\n {JsonUtility.ToJson(dto)}");
        }

        public void Load(string json, GameDataModel model)
        {
            var dto = JsonUtility.FromJson<GameDataModelSaveData>(json);


            // applying data from dto

            model.ApplySaveData(dto);


            Debug.Log($"Data read:\n {dto}");
        }
    }
}
