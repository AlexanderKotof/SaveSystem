using System;
using UnityEngine;

namespace SaveSystem.Example
{
    [Serializable]
    public struct SerializableGuid : ISerializationCallbackReceiver, IEquatable<Guid>, IEquatable<SerializableGuid>
    {
        private Guid _id;

        public bool IsEmpty => _id == Guid.Empty;

        [SerializeField, HideInInspector]
        private string _serializedId;

        public SerializableGuid(Guid guid)
        {
            _id = guid;
            _serializedId = _id.ToString();
        }

        public void OnBeforeSerialize()
        {
            Debug.Log($"Serializing {_id.ToString()}");
            _serializedId = _id.ToString();
        }

        public void OnAfterDeserialize()
        {
            _id = Guid.Parse(_serializedId);
            Debug.Log($"Deserialized {_id.ToString()}");
        }

        public bool Equals(Guid other)
        {
            return _id.Equals(other);
        }

        public bool Equals(SerializableGuid other)
        {
            return _id.Equals(other._id);
        }

        public static implicit operator Guid(SerializableGuid serializableGuid)
        {
            return serializableGuid._id;
        }
    }
}