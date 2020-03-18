using System;

using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine.Assertions;

using ManagedDataType = Unity.Kinematica.DataType;

namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        internal struct DataField
        {
            public int fieldOffset;

            public static DataField Default
            {
                get => new DataField();
            }
        }

        internal struct DataType
        {
            public enum Flag
            {
                None,
                ExpandArray,
                TopologySort
            }

            public Flag flag;

            public int hashCode;

            public int inputFieldIndex;
            public int outputFieldIndex;

            public int numInputFields;
            public int numOutputFields;

            public ExecuteFunction executeFunction;
            public IntPtr debugDrawMethod;

            DataType(ManagedDataType source)
            {
                flag = (Flag)source.flag;

                hashCode = source.hashCode;

                inputFieldIndex = -1;
                outputFieldIndex = -1;

                numInputFields = source.numInputFields;
                numOutputFields = source.numOutputFields;

                executeFunction = source.executeFunction;
                debugDrawMethod = source.debugDrawMethod;
            }

            public static DataType Create(ManagedDataType source)
            {
                return new DataType(source);
            }

            public static DataType Default
            {
                get => new DataType();
            }

            internal static MemoryRequirements GetMemoryRequirements()
            {
                var memoryRequirements = MemoryRequirements.Null;

                var numDataTypes = ManagedDataType.Types.Length;

                int numDataFields = 0;

                for (int i = 0; i < numDataTypes; ++i)
                {
                    numDataFields +=
                        ManagedDataType.Types[i].numInputFields;

                    numDataFields +=
                        ManagedDataType.Types[i].numOutputFields;
                }

                memoryRequirements +=
                    MemoryRequirements.Of<DataType>() * numDataTypes;

                memoryRequirements +=
                    MemoryRequirements.Of<DataField>() * numDataFields;

                memoryRequirements +=
                    MemoryRequirements.Of<int>() * numDataTypes;

                return memoryRequirements;
            }
        }

        void ConstructDataTypes(ref MemoryBlock memoryBlock)
        {
            int numTypes = ManagedDataType.Types.Length;

            int numFields = 0;

            dataTypes = memoryBlock.CreateArray(
                numTypes, DataType.Default);

            for (short i = 0; i < numTypes; ++i)
            {
                var source = ManagedDataType.Types[i];

                numFields += source.numInputFields;
                numFields += source.numOutputFields;

                dataTypes[i] = DataType.Create(source);
            }

            dataFields = memoryBlock.CreateArray(
                numFields, DataField.Default);

            sizeOfTable =
                memoryBlock.CreateArray(
                    numTypes, 0);

            int writeIndex = 0;

            for (short i = 0; i < numTypes; ++i)
            {
                var sourceType = ManagedDataType.Types[i];

                Assert.IsTrue(dataTypes[i].inputFieldIndex == -1);
                Assert.IsTrue(dataTypes[i].outputFieldIndex == -1);

                sizeOfTable[i] =
                    UnsafeUtility.SizeOf(sourceType.type);

                dataTypes[i].inputFieldIndex = writeIndex;

                for (short j = 0; j < sourceType.numInputFields; ++j)
                {
                    var sourceField = sourceType.inputFields[j];

                    dataFields[writeIndex++].fieldOffset =
                        UnsafeUtility.GetFieldOffset(sourceField.info);
                }

                dataTypes[i].outputFieldIndex = writeIndex;

                for (short j = 0; j < sourceType.numOutputFields; ++j)
                {
                    var sourceField = sourceType.outputFields[j];

                    dataFields[writeIndex++].fieldOffset =
                        UnsafeUtility.GetFieldOffset(sourceField.info);
                }
            }

            Assert.IsTrue(writeIndex == numFields);
        }

        internal DataTypeIndex GetDataTypeIndex<T>() where T : struct
        {
            var hashCode = BurstRuntime.GetHashCode32<T>();

            int numTypes = dataTypes.Length;

            for (short i = 0; i < numTypes; ++i)
            {
                if (dataTypes[i].hashCode == hashCode)
                {
                    return i;
                }
            }

            return DataTypeIndex.Invalid;
        }
    }
}
