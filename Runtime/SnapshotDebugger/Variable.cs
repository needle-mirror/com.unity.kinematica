using System;
using System.Reflection;
using UnityEngine;

namespace Unity.SnapshotDebugger
{
    public sealed class Variable : Serializable
    {
        public GameObject gameObject
        {
            get { return provider.gameObject; }
        }

        public object value
        {
            get { return field.GetValue(provider); }
            set { field.SetValue(provider, value); }
        }

        public SnapshotAttribute attribute
        {
            get; private set;
        }

        public string name
        {
            get { return field.Name; }
        }

        public Variable(SnapshotProvider provider, FieldInfo field, SnapshotAttribute attribute)
        {
            this.provider = provider;
            this.field = field;
            this.attribute = attribute;
        }

        public void WriteToStream(Buffer buffer)
        {
            buffer.TryWriteObject(value);
        }

        public void ReadFromStream(Buffer buffer)
        {
            var type = Type.GetType(buffer.ReadString());

            if (type == null)
            {
                throw new InvalidOperationException("Failed to read type information");
            }

            if (typeof(Serializable).IsAssignableFrom(type) == true)
            {
                if (type.IsClass)
                {
                    (value as Serializable).ReadFromStream(buffer);
                }
                else
                {
                    var serializable = Activator.CreateInstance(type) as Serializable;

                    serializable.ReadFromStream(buffer);

                    value = serializable;
                }
            }
            else
            {
                value = buffer.TryReadObject(type);
            }
        }

        SnapshotProvider provider;
        FieldInfo field;
    }
}
