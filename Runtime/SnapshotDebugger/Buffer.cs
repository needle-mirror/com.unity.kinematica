using System;
using System.Collections.Generic;

namespace Unity.SnapshotDebugger
{
    public sealed class Buffer
    {
        private List<byte> bytes = new List<byte>();

        private int readOffset = 0;

        public bool CanRead
        {
            get { return Size > 0; }
        }

        public bool EndRead
        {
            get { return readOffset >= Size; }
        }

        public int Size
        {
            get { return bytes.Count; }
        }

        public static Buffer Create()
        {
            return new Buffer();
        }

        public static Buffer Create(byte[] data)
        {
            var buffer = Create();
            buffer.bytes.AddRange(data);
            return buffer;
        }

        public void PrepareForRead()
        {
            readOffset = 0;
        }

        public void Clear()
        {
            bytes.Clear();
            readOffset = 0;
        }

        public byte[] ToArray()
        {
            return bytes.ToArray();
        }

        public void Append(byte value)
        {
            bytes.Add(value);
        }

        public void Copy(Buffer from)
        {
            foreach (byte value in from.bytes)
            {
                Append(value);
            }
        }

        public byte ReadByte()
        {
            if (EndRead)
            {
                throw new InvalidOperationException("End of buffer reached");
            }

            return bytes[readOffset++];
        }
    }
}
