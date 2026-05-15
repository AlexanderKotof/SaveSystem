using System;
using SaveSystem.Interfaces;
using UnityEngine;

namespace SaveSystem.Example
{
    [CreateAssetMenu(fileName = "Some config", menuName = "Configs/Some config")]
    public class GameConfigBase : ScriptableObject, IHasId
    {
        public Guid Id => _guid;

        [SerializeField, HideInInspector]
        private SerializableGuid _guid;

        public int someData = 42;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_guid.IsEmpty)
            {
                _guid = new SerializableGuid(Guid.NewGuid());
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        // Some base config
    }
}
