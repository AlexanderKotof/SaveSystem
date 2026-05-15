using System;
using SaveSystem.Attributes;
using SaveSystem.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;

namespace SaveSystem.Example
{
    [SaveData]
    public class SomeDataModel
    {

    }

    public class GameConfigBase : ScriptableObject, IHasId
    {
        public Guid Id => _guid;

        private SerializableGuid _guid;

        private void OnValidate()
        {
            if (_guid.IsEmpty)
            {
                _guid = new SerializableGuid(Guid.NewGuid());
            }
        }

        // Some base config
    }
}
