using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArctiumStudios.SplineTools
{
    public enum ConnectionBaseType
    {
        Custom,
        River
    }

    [Serializable]
    public class ConnectionType
    {
        [SerializeField] private ConnectionBaseType baseType;
        [SerializeField] private string customType;

        private static Dictionary<ConnectionBaseType, Dictionary<string, ConnectionType>> _cachedTypes =
            new Dictionary<ConnectionBaseType, Dictionary<string, ConnectionType>>();

        private ConnectionType(ConnectionBaseType baseType, string customType)
        {
            this.baseType = baseType;
            this.customType = customType;
        }

        public ConnectionBaseType BaseType
        {
            get { return baseType; }
        }

        public string CustomType
        {
            get { return customType; }
        }

        public static ConnectionType Of(ConnectionBaseType baseType, string customType = null)
        {
            if (!_cachedTypes.ContainsKey(baseType)) _cachedTypes.Add(baseType, new Dictionary<string, ConnectionType>());
            var cachedCustomTypes = _cachedTypes[baseType];
            var nonNullCustomType = customType == null ? "" : customType;
            if (cachedCustomTypes.ContainsKey(nonNullCustomType)) return cachedCustomTypes[nonNullCustomType];

            var connectionType = new ConnectionType(baseType, customType);
            cachedCustomTypes.Add(nonNullCustomType, connectionType);

            return connectionType;
        }


        protected bool Equals(ConnectionType other)
        {
            return baseType == other.baseType && string.Equals(customType, other.customType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConnectionType) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) baseType + 397) ^ (customType != null ? customType.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return baseType + " " + (customType == null ? "" : customType);
        }
    }
}