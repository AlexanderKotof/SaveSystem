using System;
using UnityEngine;

namespace SaveSystem.Example
{
    [Serializable]
    public struct SerializableGuid : IEquatable<Guid>, IEquatable<SerializableGuid>
    {
        [SerializeField]
        private byte[] _serializedId;
        private Guid? _id;

        public bool IsEmpty => _serializedId.Length == 0 || Value == Guid.Empty;

        public Guid Value
        {
            get
            {
                _id ??= new Guid(_serializedId);
                return _id.Value;
            }
        }

        public SerializableGuid(Guid guid)
        {
            _id = guid;
            _serializedId = guid.ToByteArray();
        }

        public SerializableGuid(byte[] guid)
        {
            _serializedId = guid;
            _id = new Guid(_serializedId);
        }

        public bool Equals(Guid other)
        {
            return Value.Equals(other);
        }

        public bool Equals(SerializableGuid other)
        {
            return Value.Equals(other.Value);
        }

        public static implicit operator Guid(SerializableGuid serializableGuid)
        {
            return serializableGuid.Value;
        }
    }
}
