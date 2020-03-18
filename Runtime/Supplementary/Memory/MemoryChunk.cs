using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.SnapshotDebugger;

using UnityEngine.Assertions;

using Buffer = Unity.SnapshotDebugger.Buffer;

namespace Unity.Kinematica
{
    internal unsafe struct MemoryChunk : IDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        void* ptr;
        int numBytesAllocated;

        Allocator allocator;

        int writeOffset;
        int version;

        ushort tickFrame;

        short numElements;

        MemoryIdentifier root;

        public struct Header
        {
            public const ushort Dirty = 1 << 15;
            public const ushort Disposed = 1 << 14;
            public const ushort Succeeded = 1 << 13;
            public const ushort MarkedForDelete = 1 << 12;
            public const ushort FlagMask = Dirty | Disposed | Succeeded | MarkedForDelete;

            public ushort tickFrame;
            public short length;

            public MemoryIdentifier self;
            public MemoryIdentifier parent;
            public MemoryIdentifier firstChild;
            public MemoryIdentifier nextSibling;
            public MemoryIdentifier previousSibling;

            public DataTypeIndex typeIndex;

            public bool IsDirty => (tickFrame & Dirty) == Dirty;
            public bool IsDisposed => (tickFrame & Disposed) == Disposed;
            public bool HasSucceeded => (tickFrame & Succeeded) == Succeeded;
            public bool IsMarkedForDelete => (tickFrame & MarkedForDelete) == MarkedForDelete;

            public void Dispose()
            {
                tickFrame |= unchecked((ushort)Disposed);
            }

            public void ClearDirty()
            {
                tickFrame &= unchecked((ushort)~Dirty);
            }

            public void SetDirty()
            {
                tickFrame |= unchecked((ushort)Dirty);
            }

            public void ClearSucceeded()
            {
                tickFrame &= unchecked((ushort)~Succeeded);
            }

            public void SetSucceeded()
            {
                tickFrame |= unchecked((ushort)Succeeded);
            }

            public void MarkForDelete()
            {
                tickFrame |= unchecked((ushort)MarkedForDelete);
            }

            public int GetNextTickFrame()
            {
                var bitMask = unchecked((ushort)~FlagMask);

                var nextTickFrame = (ushort)((tickFrame + 1) & bitMask);

                return nextTickFrame;
            }

            public static Header Create(MemoryIdentifier identifier)
            {
                return new Header
                {
                    self = identifier
                };
            }

            public static Header Invalid => Create(MemoryIdentifier.Invalid);
        }

        public struct TocEntry
        {
            public int offset;

            public bool IsValid => offset != Invalid;

            public static implicit operator int(TocEntry entry)
            {
                return entry.offset;
            }

            public static implicit operator TocEntry(int offset)
            {
                return Create(offset);
            }

            public static TocEntry Create(int offset)
            {
                return new TocEntry
                {
                    offset = offset
                };
            }

            public static TocEntry Invalid => - 1;
        }

        public bool IsValid => ptr != null;

        public int TickFrame => tickFrame;

        public int Version => version;

        MemoryIdentifier CreateIdentifier()
        {
            int sizeOfToc = sizeof(TocEntry);

            var memoryPtr = (TocEntry*)((byte*)
                ptr + numBytesAllocated - sizeOfToc);

            for (short i = 0; i < numElements; ++i)
            {
                if (memoryPtr[-i].offset < 0)
                {
                    return i;
                }
            }

            int numElementsPlusOne = numElements + 1;

            int numBytesToc = sizeOfToc * numElementsPlusOne;
            int numBytesUsed = writeOffset + numBytesToc;
            int numBytesRemaining = numBytesAllocated - numBytesUsed;

            if (numBytesRemaining < 0)
            {
                Grow(sizeOfToc);
            }

            memoryPtr[-numElements].offset = -1;

            return numElements++;
        }

        public void TickRecursive(MemoryIdentifier identifier)
        {
            Tick(identifier);

            var node = FirstChild(identifier);

            while (node.IsValid)
            {
                TickRecursive(node);

                node = NextSibling(node);
            }
        }

        public void Tick(MemoryIdentifier identifier)
        {
            var bitMask = unchecked((ushort)~Header.FlagMask);

            var nextTickFrame = (ushort)((this.tickFrame + 1) & bitMask);

            var header = GetHeader(identifier);

            var tickFrame = header->tickFrame;

            var bits = tickFrame & Header.FlagMask;

            header->tickFrame = (ushort)(nextTickFrame | bits);
        }

        TocEntry* GetTocEntry(MemoryIdentifier identifier)
        {
            var memoryPtr = (byte*)ptr + numBytesAllocated;
            memoryPtr -= sizeof(TocEntry) * (identifier + 1);
            return (TocEntry*)memoryPtr;
        }

        public Header* GetHeader(MemoryIdentifier identifier)
        {
            Assert.IsTrue(identifier.IsValid);

            return (Header*)((byte*)ptr + GetTocEntry(identifier)->offset);
        }

        public byte* GetPayload(MemoryIdentifier identifier)
        {
            return (byte*)(GetHeader(identifier) + 1);
        }

        int GetMemoryOffset(byte* memoryPtr)
        {
            return (int)(memoryPtr - (byte*)ptr);
        }

        public MemoryIdentifier Allocate<T>(T value, DataTypeIndex typeIndex, MemoryIdentifier parent) where T : struct
        {
            var identifier = CreateIdentifier();

            var memoryPtr = CreateHeader<T>(
                identifier, parent, typeIndex);

            UnsafeUtility.CopyStructureToPtr(
                ref value, memoryPtr);

            int sizeOfType = UnsafeUtility.SizeOf<T>();
            int alignedSizeOfType = Memory.Align(sizeOfType, 4);

            memoryPtr += alignedSizeOfType;

            Assert.IsTrue(GetMemoryOffset(memoryPtr) == writeOffset);

            return identifier;
        }

        public MemoryIdentifier AllocateArray<T>(int length, DataTypeIndex typeIndex, MemoryIdentifier parent) where T : struct
        {
            var identifier = CreateIdentifier();

            Assert.IsTrue(length <= short.MaxValue);

            var numElements = (short)length;

            var memoryPtr = CreateHeader<T>(
                identifier, parent,
                typeIndex, numElements);

            return identifier;
        }

        public MemoryIdentifier Allocate<T>(MemoryArray<T> source, DataTypeIndex typeIndex, MemoryIdentifier parent) where T : struct
        {
            var identifier = CreateIdentifier();

            Assert.IsTrue(source.Length <= short.MaxValue);

            var numElements = (short)source.Length;

            var memoryPtr = CreateHeader<T>(
                identifier, parent,
                typeIndex, numElements);

            int sizeOfType = UnsafeUtility.SizeOf<T>();
            int alignedSizeOfType = Memory.Align(sizeOfType, 4);

            for (int i = 0; i < numElements; ++i)
            {
                var value = source[i];

                UnsafeUtility.CopyStructureToPtr(
                    ref value, memoryPtr);

                memoryPtr += alignedSizeOfType;
            }

            Assert.IsTrue(GetMemoryOffset(memoryPtr) == writeOffset);

            return identifier;
        }

        public MemoryIdentifier Allocate<T>(NativeList<T> source, DataTypeIndex typeIndex, MemoryIdentifier parent) where T : struct
        {
            var identifier = CreateIdentifier();

            Assert.IsTrue(source.Length <= short.MaxValue);

            var numElements = (short)source.Length;

            var memoryPtr = CreateHeader<T>(
                identifier, parent,
                typeIndex, numElements);

            int sizeOfType = UnsafeUtility.SizeOf<T>();
            int alignedSizeOfType = Memory.Align(sizeOfType, 4);

            for (int i = 0; i < numElements; ++i)
            {
                var value = source[i];

                UnsafeUtility.CopyStructureToPtr(
                    ref value, memoryPtr);

                memoryPtr += alignedSizeOfType;
            }

            Assert.IsTrue(GetMemoryOffset(memoryPtr) == writeOffset);

            return identifier;
        }

        public MemoryRef<T> GetByType<T>(DataTypeIndex typeIndex, MemoryIdentifier identifier) where T : struct
        {
            Assert.IsTrue(identifier.IsValid);

            var headerPtr = GetHeader(identifier);

            if (headerPtr->typeIndex == typeIndex)
            {
                var memoryPtr = (byte*)(++headerPtr);

                return new MemoryRef<T>
                {
                    ptr = memoryPtr
                };
            }

            var node = FirstChild(identifier);

            while (node.IsValid)
            {
                var result = GetByType<T>(typeIndex, node);

                if (result.IsValid)
                {
                    return result;
                }

                node = NextSibling(node);
            }

            return MemoryRef<T>.Null;
        }

        public MemoryRef<T> GetRef<T>(MemoryIdentifier identifier) where T : struct
        {
            Assert.IsTrue(identifier.IsValid);

            var headerPtr = GetHeader(identifier);

            var memoryPtr = (byte*)(++headerPtr);

            return new MemoryRef<T>
            {
                ptr = memoryPtr
            };
        }

        public MemoryArray<T> GetArray<T>(MemoryIdentifier identifier) where T : struct
        {
            Assert.IsTrue(identifier.IsValid);

            var headerPtr = GetHeader(identifier);

            var length = headerPtr->length;

            var memoryPtr = (byte*)(++headerPtr);

            return new MemoryArray<T>
            {
                ptr = memoryPtr,
                length = length
            };
        }

        public void MarkForDelete(MemoryIdentifier identifier)
        {
            Header* header = GetHeader(identifier);
            header->MarkForDelete();
        }

        public static MemoryHeader<MemoryChunk> Create(MemoryRequirements memoryRequirements, Allocator allocator)
        {
            MemoryHeader<MemoryChunk> result;

            var memoryBlock = MemoryBlock.Create(
                MemoryRequirements.Of<MemoryChunk>(),
                allocator, out result);

            result.Ref.Construct(
                ref memoryBlock, memoryRequirements, allocator);

            Assert.IsTrue(memoryBlock.IsComplete);

            return result;
        }

        void Construct(ref MemoryBlock memoryBlock, MemoryRequirements memoryRequirements, Allocator allocator)
        {
            //
            // Native allocation is only valid for Temp, Job and Persistent.
            //

            if (allocator <= Allocator.None)
            {
                throw new ArgumentException(
                    "Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            }

            //
            // Make sure we cannot allocate more than int.MaxValue (2,147,483,647 bytes)
            // because the underlying UnsafeUtility.Malloc is expecting a int.
            // TODO: change UnsafeUtility.Malloc to accept a UIntPtr length instead to match C++ API
            //

            if (memoryRequirements.Size > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    $"Memory requirements cannot exceed {int.MaxValue} bytes");
            }

            void* memoryPtr = UnsafeUtility.Malloc(
                memoryRequirements.Size, memoryRequirements.Alignment, allocator);

            this.allocator = allocator;

            ptr = memoryPtr;
            numBytesAllocated = memoryRequirements.Size;

            root = MemoryIdentifier.Invalid;

            Assert.IsTrue(Memory.IsAligned(
                ptr, memoryRequirements.Alignment));

            Poison();
        }

        public void Dispose()
        {
            if (!UnsafeUtility.IsValidAllocator(allocator))
            {
                throw new InvalidOperationException("The memory chunk can not be disposed because it was not allocated with a valid allocator.");
            }

            UnsafeUtility.Free(ptr, allocator);

            ptr = null;
        }

        public unsafe void WriteToStream(Buffer buffer)
        {
            buffer.Write(numBytesAllocated);
            buffer.Write((short)tickFrame);
            buffer.Write(root);

            buffer.Write(writeOffset);
            buffer.Write(version);

            if (writeOffset > 0)
            {
                var byteArray = new byte[writeOffset];
                fixed(byte* dst = &byteArray[0])
                {
                    UnsafeUtility.MemCpy(dst, ptr, writeOffset);
                }
                buffer.Write(byteArray);
            }

            buffer.Write(numElements);

            if (numElements > 0)
            {
                int sizeOfToc = sizeof(TocEntry);

                int numBytesToc =
                    sizeof(TocEntry) * numElements;

                var memoryPtr = (TocEntry*)((byte*)
                    ptr + numBytesAllocated);

                memoryPtr -= numElements;

                var byteArray = new byte[numBytesToc];
                fixed(byte* dst = &byteArray[0])
                {
                    UnsafeUtility.MemCpy(dst, memoryPtr, numBytesToc);
                }
                buffer.Write(byteArray);
            }
        }

        public unsafe void ReadFromStream(Buffer buffer)
        {
            int numBytes = buffer.Read32();
            Assert.IsTrue(numBytesAllocated >= numBytes);

            tickFrame = (ushort)buffer.Read16();
            root = buffer.Read16();

            writeOffset = buffer.Read32();
            version = buffer.Read32();

            if (writeOffset > 0)
            {
                var byteArray = buffer.ReadBytes(writeOffset);
                fixed(byte* src = &byteArray[0])
                {
                    UnsafeUtility.MemCpy(ptr, src, writeOffset);
                }
            }

            numElements = buffer.Read16();

            if (numElements > 0)
            {
                int sizeOfToc = sizeof(TocEntry);

                int numBytesToc =
                    sizeof(TocEntry) * numElements;

                var memoryPtr = (TocEntry*)((byte*)
                    ptr + numBytesAllocated);

                memoryPtr -= numElements;

                var byteArray = buffer.ReadBytes(numBytesToc);
                fixed(byte* src = &byteArray[0])
                {
                    UnsafeUtility.MemCpy(memoryPtr, src, numBytesToc);
                }
            }
        }

        public MemoryIdentifier Root => root;

        public MemoryIdentifier Parent(MemoryIdentifier identifier)
        {
            return GetHeader(identifier)->parent;
        }

        public MemoryIdentifier FirstChild(MemoryIdentifier identifier)
        {
            return GetHeader(identifier)->firstChild;
        }

        public MemoryIdentifier NextSibling(MemoryIdentifier identifier)
        {
            return GetHeader(identifier)->nextSibling;
        }

        public int NumChildren(MemoryIdentifier identifier)
        {
            int numChildren = 0;

            var node = FirstChild(identifier);

            while (node.IsValid)
            {
                numChildren++;

                node = NextSibling(node);
            }

            return numChildren;
        }

        public MemoryIdentifier Child(MemoryIdentifier identifier, int index)
        {
            int numChildren = 0;

            var node = FirstChild(identifier);

            while (node.IsValid)
            {
                if (numChildren == index)
                {
                    return node;
                }

                numChildren++;

                node = NextSibling(node);
            }

            return MemoryIdentifier.Invalid;
        }

        public void BringToFront(MemoryIdentifier identifier)
        {
            var header = GetHeader(identifier);

            var parent = header->parent;

            Assert.IsTrue(parent.IsValid);

            var parentHeader = GetHeader(parent);

            if (parentHeader->firstChild != identifier)
            {
                if (header->previousSibling.IsValid)
                {
                    var previous = GetHeader(header->previousSibling);

                    Assert.IsTrue(previous->nextSibling == header->self);

                    previous->nextSibling = header->nextSibling;
                }

                if (header->nextSibling.IsValid)
                {
                    var next = GetHeader(header->nextSibling);

                    Assert.IsTrue(next->previousSibling == header->self);

                    next->previousSibling = header->previousSibling;
                }

                header->nextSibling = parentHeader->firstChild;

                Assert.IsTrue(header->nextSibling.IsValid);

                GetHeader(header->nextSibling)->previousSibling = identifier;

                header->previousSibling = MemoryIdentifier.Invalid;

                parentHeader->firstChild = identifier;
            }
        }

        public MemoryIdentifier Next(MemoryIdentifier identifier)
        {
            return NextUntil(identifier, root);
        }

        public MemoryIdentifier NextUntil(MemoryIdentifier current, MemoryIdentifier identifier)
        {
            var header = GetHeader(current);

            if (header->firstChild != MemoryIdentifier.Invalid)
            {
                return header->firstChild;
            }
            else
            {
                while (header->nextSibling == MemoryIdentifier.Invalid)
                {
                    if (header->self == identifier)
                    {
                        return MemoryIdentifier.Invalid;
                    }

                    header = GetHeader(header->parent);
                }

                return header->nextSibling;
            }
        }

        MemoryIdentifier NextUntilUp(MemoryIdentifier current, MemoryIdentifier identifier)
        {
            var header = GetHeader(current);

            while (header->nextSibling == MemoryIdentifier.Invalid)
            {
                if (header->self == identifier)
                {
                    return MemoryIdentifier.Invalid;
                }

                header = GetHeader(header->parent);
            }

            return header->nextSibling;
        }

        byte* CreateHeader<T>(MemoryIdentifier identifier, MemoryIdentifier parent, DataTypeIndex typeIndex, short numElements = 1) where T : struct
        {
            int sizeOfType = UnsafeUtility.SizeOf<T>();
            int sizeOfHeader = sizeof(Header);

            Assert.IsTrue(sizeOfHeader == 16);

            int alignedSizeOfType = Memory.Align(sizeOfType, 4);
            int sizeOfElements = alignedSizeOfType * numElements;

            int numBytesRequired = sizeOfElements + sizeOfHeader;

            Grow(numBytesRequired);

            GetTocEntry(identifier)->offset = writeOffset;

            var header = (Header*)((byte*)ptr + writeOffset);

            var bitMask = unchecked((ushort)~Header.FlagMask);
            var tickFrame = (ushort)((this.tickFrame + 1) & bitMask);
            header->tickFrame = (ushort)(tickFrame | Header.Dirty);

            header->length = numElements;
            header->self = identifier;
            header->typeIndex = typeIndex;

            header->nextSibling = MemoryIdentifier.Invalid;
            header->previousSibling = MemoryIdentifier.Invalid;
            header->firstChild = MemoryIdentifier.Invalid;

            header->parent = parent;

            if (parent.IsValid)
            {
                var parentHeader = GetHeader(parent);

                if (parentHeader->firstChild.IsValid)
                {
                    var sibling = parentHeader->firstChild;
                    var siblingPtr = GetHeader(sibling);

                    while (siblingPtr->nextSibling.IsValid)
                    {
                        sibling = siblingPtr->nextSibling;
                        siblingPtr = GetHeader(sibling);
                    }

                    header->previousSibling = sibling;
                    siblingPtr->nextSibling = identifier;
                }
                else
                {
                    parentHeader->firstChild = identifier;
                }
            }
            else
            {
                root = identifier;
            }

            byte* memoryPtr = (byte*)(header + 1);

            UnsafeUtility.MemSet(
                memoryPtr, 0, sizeOfElements);

            writeOffset += numBytesRequired;

            version++;

            return memoryPtr;
        }

        public void CopyFrom(ref MemoryChunk memoryChunk)
        {
            if (numBytesAllocated != memoryChunk.numBytesAllocated)
            {
                numBytesAllocated = memoryChunk.numBytesAllocated;

                UnsafeUtility.Free(ptr, allocator);

                ptr = UnsafeUtility.Malloc(
                    numBytesAllocated, 4, allocator);
            }

            Assert.IsTrue(numBytesAllocated == memoryChunk.numBytesAllocated);

            UnsafeUtility.MemCpy(
                ptr, memoryChunk.ptr, numBytesAllocated);

            writeOffset = memoryChunk.writeOffset;
            tickFrame = memoryChunk.tickFrame;
            numElements = memoryChunk.numElements;
            root = memoryChunk.root;
        }

        public unsafe void CopyOverridesTo(ref MemoryChunk memoryChunk, MemoryArray<int> sizeOfTable)
        {
            byte* readPtr = (byte*)ptr;

            byte* writePtr = readPtr + writeOffset;

            int sizeOfHeader = sizeof(Header);

            while (readPtr < writePtr)
            {
                var header = (Header*)readPtr;

                var length = header->length;

                var typeIndex = header->typeIndex.value;

                var numBytesPayload =
                    sizeOfTable[typeIndex] * length;

                numBytesPayload = Memory.Align(numBytesPayload, 4);

                var numBytesTotal = sizeOfHeader + numBytesPayload;

                if (header->IsDirty)
                {
                    int offset = (int)(readPtr - (byte*)ptr);

                    byte* destination =
                        (byte*)memoryChunk.ptr + offset;

                    UnsafeUtility.MemCpy(
                        destination, readPtr, numBytesTotal);
                }

                readPtr += numBytesTotal;

                Assert.IsTrue(readPtr <= writePtr);
            }
        }

        internal unsafe struct GarbageCollector
        {
            MemoryArray<int> sizeOfTable;

            TocEntry* tocPtr;

            byte* readPtr;
            byte* writePtr;

            byte* prunePtr;

            byte* beginPtr;
            byte* endPtr;

            byte* basePtr;

            public int Execute(int writeOffset, ushort tickFrame)
            {
                //
                // Sweep over all allocated elements and compact payloads
                // for any element where its tick frame does not match
                // the current tick frame.
                //

                readPtr = basePtr;

                writePtr = readPtr + writeOffset;

                int sizeOfHeader = sizeof(Header);

                //
                // Iterate all allocated items
                //

                while (readPtr < writePtr)
                {
                    //
                    // Retrieve number of bytes required to read over this item
                    //

                    var header = (Header*)readPtr;

                    var length = header->length;

                    var typeIndex = header->typeIndex.value;

                    var numBytesPayload =
                        sizeOfTable[typeIndex] * length;

                    numBytesPayload = Memory.Align(numBytesPayload, 4);

                    var numBytesTotal = sizeOfHeader + numBytesPayload;

                    //
                    // Check if this item has been disposed
                    //

                    if (header->IsDisposed)
                    {
                        //
                        // Mark the corresponding toc entry as invalid
                        //

                        tocPtr[-header->self].offset = -1;

                        //
                        // We remember the current cursor position for later pruning.
                        //

                        Prune();

                        if (prunePtr == null)
                        {
                            prunePtr = readPtr;

                            beginPtr = null;
                        }
                    }
                    else
                    {
                        if (beginPtr == null)
                        {
                            beginPtr = readPtr;
                            endPtr = readPtr;
                        }

                        endPtr += numBytesTotal;
                    }

                    readPtr += numBytesTotal;

                    Assert.IsTrue(readPtr <= writePtr);
                }

                Prune();

                if (prunePtr != null)
                {
                    writePtr = prunePtr;

                    prunePtr = null;
                }

                return (int)(writePtr - basePtr);
            }

            void Prune()
            {
                if (prunePtr != null)
                {
                    if (beginPtr > prunePtr)
                    {
                        var numBytesToCopy = (int)(writePtr - beginPtr);

                        UnsafeUtility.MemCpy(
                            prunePtr, beginPtr, numBytesToCopy);

                        var numBytesToRelocate = (int)(endPtr - beginPtr);

                        Relocate(prunePtr, numBytesToRelocate);

                        var numBytesReduced =
                            (int)(beginPtr - prunePtr);

                        writePtr -= numBytesReduced;
                        readPtr -= numBytesReduced;

                        prunePtr = null;
                    }
                }
            }

            void Relocate(byte* ptr, int numBytesToRead)
            {
                int sizeOfHeader = sizeof(Header);

                while (numBytesToRead > 0)
                {
                    var header = (Header*)ptr;

                    var length = header->length;

                    var typeIndex = header->typeIndex.value;

                    var numBytesPayload =
                        sizeOfTable[typeIndex] * length;

                    numBytesPayload = Memory.Align(numBytesPayload, 4);

                    var numBytesTotal = sizeOfHeader + numBytesPayload;

                    int offset = (int)(ptr - basePtr);

                    tocPtr[-header->self].offset = offset;

                    ptr += numBytesTotal;

                    numBytesToRead -= numBytesTotal;

                    Assert.IsTrue(numBytesToRead >= 0);
                }
            }

            public static GarbageCollector Create(MemoryArray<int> sizeOfTable, byte* basePtr, TocEntry* tocPtr)
            {
                return new GarbageCollector
                {
                    sizeOfTable = sizeOfTable,
                    basePtr = basePtr,
                    tocPtr = tocPtr
                };
            }
        }

        void InvalidateTree(MemoryIdentifier identifier)
        {
            var node = identifier;

            while (node.IsValid)
            {
                GetHeader(node)->Dispose();

                node = NextUntil(node, identifier);
            }
        }

        void Unlink(MemoryIdentifier identifier)
        {
            var header = GetHeader(identifier);

            var parent = header->parent;

            Assert.IsTrue(parent.IsValid);

            var parentHeader = GetHeader(parent);

            if (parentHeader->firstChild == identifier)
            {
                parentHeader->firstChild = header->nextSibling;
            }

            if (header->previousSibling.IsValid)
            {
                var previous = GetHeader(header->previousSibling);

                Assert.IsTrue(previous->nextSibling == header->self);

                previous->nextSibling = header->nextSibling;
            }

            if (header->nextSibling.IsValid)
            {
                var next = GetHeader(header->nextSibling);

                Assert.IsTrue(next->previousSibling == header->self);

                next->previousSibling = header->previousSibling;
            }

            header->parent = MemoryIdentifier.Invalid;
            header->nextSibling = MemoryIdentifier.Invalid;
            header->previousSibling = MemoryIdentifier.Invalid;
        }

        public void SweepAndPrune(MemoryArray<int> sizeOfTable)
        {
            Verify(sizeOfTable);

            //
            // Increment tick frame and unlink all elements
            // from the hierarchy which haven't been ticked this frame.
            //

            Tick(root);

            var bitMask = unchecked((ushort)~Header.FlagMask);

            tickFrame = (ushort)((this.tickFrame + 1) & bitMask);

            var node = root;

            Assert.IsTrue(node.IsValid);

            bool gcPassRequired = false;

            while (node.IsValid)
            {
                var header = GetHeader(node);

                if (header->IsMarkedForDelete || (ushort)(header->tickFrame & bitMask) != tickFrame)
                {
                    gcPassRequired = true;

                    var next = NextUntilUp(node, root);

                    Unlink(node);

                    InvalidateTree(node);

                    node = next;
                }
                else
                {
                    node = NextUntil(node, root);
                }
            }

            if (gcPassRequired)
            {
                //
                // Prepare and execute the garbage collector
                //

                int sizeOfToc = sizeof(TocEntry);

                var tocPtr = (TocEntry*)((byte*)
                    ptr + numBytesAllocated - sizeOfToc);

                var gc = GarbageCollector.Create(
                    sizeOfTable, (byte*)ptr, tocPtr);

                writeOffset = gc.Execute(writeOffset, tickFrame);

                version++;

                //
                // Debug prologue
                //

                Poison();

                Verify(sizeOfTable);
            }
        }

        void Verify(MemoryArray<int> sizeOfTable)
        {
            int sizeOfToc = sizeof(TocEntry);

            var tocPtr = (TocEntry*)((byte*)
                ptr + numBytesAllocated - sizeOfToc);

            byte* readPtr = (byte*)ptr;

            byte* writePtr = readPtr + writeOffset;

            int sizeOfHeader = sizeof(Header);

            while (readPtr < writePtr)
            {
                int offset = (int)(readPtr - (byte*)ptr);

                var header = (Header*)readPtr;

                var length = header->length;

                var typeIndex = header->typeIndex.value;

                var numBytesPayload =
                    sizeOfTable[typeIndex] * length;

                numBytesPayload = Memory.Align(numBytesPayload, 4);

                var numBytesTotal = sizeOfHeader + numBytesPayload;

                Assert.IsTrue(tocPtr[-header->self].offset == offset);

                readPtr += numBytesTotal;

                Assert.IsTrue(readPtr <= writePtr);
            }

            for (short i = 0; i < numElements; ++i)
            {
                var tocEntry = GetTocEntry(i);

                if (tocEntry->offset >= 0)
                {
                    Assert.IsTrue(GetHeader(i)->self == i);
                }
            }
        }

        void Grow(int numBytesRequired)
        {
            int numBytesToc = sizeof(TocEntry) * numElements;
            int numBytesUsed = writeOffset + numBytesToc;
            int numBytesRemaining = numBytesAllocated - numBytesUsed;
            int numBytesToGrow = numBytesRequired - numBytesRemaining;

            if (numBytesToGrow > 0)
            {
                int length = numBytesAllocated +
                    math.max(numBytesAllocated, numBytesToGrow);

                void* memoryPtr = UnsafeUtility.Malloc(
                    length, 4, allocator);

                UnsafeUtility.MemSet(memoryPtr, 0xCD, length);

                UnsafeUtility.MemCpy(memoryPtr, ptr, writeOffset);

                var oldToc = (byte*)ptr + numBytesAllocated - numBytesToc;
                var newToc = (byte*)memoryPtr + length - numBytesToc;

                UnsafeUtility.MemCpy(newToc, oldToc, numBytesToc);

                UnsafeUtility.Free(ptr, allocator);

                ptr = memoryPtr;
                numBytesAllocated = length;
            }
        }

        void Poison()
        {
            int numBytesToc = sizeof(TocEntry) * numElements;
            int numBytesUsed = writeOffset + numBytesToc;
            int numBytesPoison = numBytesAllocated - numBytesUsed;

            if (numBytesPoison > 0)
            {
                UnsafeUtility.MemSet((byte*)ptr + writeOffset, 0xCD, numBytesPoison);
            }
        }
    }
}
