using UnityEngine.Assertions;
using Unity.SnapshotDebugger;
using Unity.Mathematics;
using Unity.Collections;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        //
        // Kinematica Public API
        //

        /// <summary>
        /// Denotes the identifier of the root task.
        /// </summary>
        /// <remarks>
        /// All user defined tasks and their corresponding input
        /// and output data will be a direct or indirect child of
        /// the root task.
        /// </remarks>
        public MemoryIdentifier Root => memoryChunk.Ref.Root;

        internal void UpdateFrameCount(int frameCount)
        {
            this.frameCount = frameCount;
        }

        /// <summary>
        /// Ticks the entire task graph during this frame.
        /// </summary>
        /// <remarks>
        /// The task graph can contain any number of user defined tasks
        /// and their corresponding input and output data.
        /// <para>
        /// Each node (task or data) has to be actively kept alive.
        /// This process is called "ticking" in the context of Kinematica
        /// and requires one of the Tick() methods to be called.
        /// Each node that does not receive a Tick() call will automatically
        /// be removed before the task graph executes.
        /// </para>
        /// </remarks>
        public void Tick()
        {
            memoryChunk.Ref.TickRecursive(
                memoryChunk.Ref.Root);
        }

        /// <summary>
        /// Checks if a memory identifer is valid and bound to valid data (Task, array...) in the synthesizer.
        /// You should always use this function before calling GetRef(), GetByType(), GetArray() functions if you are unsure
        /// the memory identifier is valid
        /// </summary>
        /// <param name="identifier">Memory identifier to check for validity.</param>
        /// <returns>True if a memory identifer is valid and bound to valid data in the synthesizer, false otherwise.</returns>
        public bool IsIdentifierValid(MemoryIdentifier identifier)
        {
            return memoryChunk.Ref.IsIdentifierBound(identifier);
        }

        /// <summary>
        /// Retrieves the identifier of the parent node w.r.t. the identifier passed as argument.
        /// </summary>
        /// <remarks>
        /// Each node (task or data) in the task graph is uniquely identified
        /// via a memory identifier. Tasks are arranged in a hierarchical
        /// fashion.
        /// </remarks>
        /// <param name="identifier">Memory identifier for which the parent identifier should be retrieved.</param>
        /// <returns>The memory identifier that corresponds to the parent node of the identifier passed as argument.</returns>
        public MemoryIdentifier Parent(MemoryIdentifier identifier)
        {
            return memoryChunk.Ref.Parent(identifier);
        }

        /// <summary>
        /// Ticks a section of the task graph.
        /// </summary>
        /// <remarks>
        /// This method ensure that all nodes that are direct or indirect children
        /// of the memory identifer passed as argument are considerd to be valid
        /// in this frame.
        /// </remarks>
        /// <param name="identifier">Memory identifier that indicates the parent node of the task graph section to be kept alive.</param>
        public void Tick(MemoryIdentifier identifier)
        {
            if (!identifier.IsValid)
            {
                identifier = memoryChunk.Ref.Root;
            }

            memoryChunk.Ref.TickRecursive(identifier);
        }

        /// <summary>
        /// Retrieves a reference to the task or data instance that corresponds to the memory identifier passed as argument.
        /// </summary>
        /// <param name="identifier">Memory identifier for which the instance data should be retrieved.</param>
        public MemoryRef<T> GetRef<T>(MemoryIdentifier identifier) where T : struct
        {
            return memoryChunk.Ref.GetRef<T>(identifier);
        }

        /// <summary>
        /// Retrieves a reference to the data array that corresponds to the memory identifier passed as argument.
        /// </summary>
        /// <param name="identifier">Memory identifier for which the data array should be retrieved.</param>
        public MemoryArray<T> GetArray<T>(MemoryIdentifier identifier) where T : struct
        {
            return memoryChunk.Ref.GetArray<T>(identifier);
        }

        /// <summary>
        /// Searches for a child node based on its type.
        /// </summary>
        /// <remarks>
        /// Retrieves a reference to a data type that is a direct or indirect child
        /// of the identifier passed as argument, subject to a type parameter.
        /// </remarks>
        /// <param name="parent">The start node to be used for the search.</param>
        /// <returns>Memory reference of the result.</returns>
        public MemoryRef<T> GetByType<T>(MemoryIdentifier parent) where T : struct
        {
            var typeIndex = GetDataTypeIndex<T>();

            Assert.IsTrue(typeIndex.IsValid);

            Assert.IsTrue(parent.IsValid);

            return memoryChunk.Ref.GetByType<T>(typeIndex, parent);
        }

        /// <summary>
        /// Creates a new node in the task graph. Nodes represent either data or executable tasks.
        /// </summary>
        /// <remarks>
        /// The new node will always be created as a child of the task graph root node.
        /// Newly created nodes will be valid for the current frame but have to
        /// get ticked during subsequent frames in order for them to stay alive.
        /// </remarks>
        /// <param name="task">Instance of the new node.</param>
        /// <returns>Memory identifier that corresponds to the newly created node.</returns>
        public MemoryIdentifier Allocate<T>(T task) where T : struct
        {
            return Allocate(task, memoryChunk.Ref.Root);
        }

        /// <summary>
        /// Creates a new node in the task graph. Nodes represent either data or executable tasks.
        /// </summary>
        /// <remarks>
        /// The new node will be created as a child of the parent node passed as argument.
        /// Newly created nodes will be valid for the current frame but have to
        /// get ticked during subsequent frames in order for them to stay alive.
        /// </remarks>
        /// <param name="task">Instance of the new node.</param>
        /// <param name="parent">Memory identifier of the parent for the new node.</param>
        /// <returns>Memory identifier that corresponds to the newly created node.</returns>
        public MemoryIdentifier Allocate<T>(T task, MemoryIdentifier parent) where T : struct
        {
            Assert.IsTrue(parent.IsValid);

            var typeIndex = GetDataTypeIndex<T>();

            Assert.IsTrue(typeIndex.IsValid);

            return memoryChunk.Ref.Allocate(
                task, typeIndex, parent);
        }

        /// <summary>
        /// Creates a new data array in the task graph.
        /// </summary>
        /// <remarks>
        /// The new data array will be created as a child of the parent node passed as argument.
        /// Newly created nodes will be valid for the current frame but have to
        /// get ticked during subsequent frames in order for them to stay alive.
        /// </remarks>
        /// <param name="length">Length of the data array to be created.</param>
        /// <param name="parent">Memory identifier of the parent for the new node.</param>
        /// <returns>Memory identifier that corresponds to the newly created node.</returns>
        public MemoryIdentifier AllocateArray<T>(int length, MemoryIdentifier parent) where T : struct
        {
            Assert.IsTrue(parent.IsValid);
            Assert.IsTrue(length <= short.MaxValue);

            var typeIndex = GetDataTypeIndex<T>();

            Assert.IsTrue(typeIndex.IsValid);

            return
                memoryChunk.Ref.AllocateArray<T>(
                length, typeIndex, parent);
        }

        /// <summary>
        /// Creates a new data array in the task graph.
        /// </summary>
        /// <remarks>
        /// The new data array will be created as a child of the parent node passed as argument.
        /// Newly created nodes will be valid for the current frame but have to
        /// get ticked during subsequent frames in order for them to stay alive.
        /// </remarks>
        /// <param name="source">Array which will be copied into the newly created data array.</param>
        /// <param name="parent">Memory identifier of the parent for the new node.</param>
        /// <returns>Memory identifier that corresponds to the newly created node.</returns>
        public MemoryIdentifier Allocate<T>(MemoryArray<T> source, MemoryIdentifier parent) where T : struct
        {
            Assert.IsTrue(parent.IsValid);
            Assert.IsTrue(source.Length <= short.MaxValue);

            var typeIndex = GetDataTypeIndex<T>();

            Assert.IsTrue(typeIndex.IsValid);

            return
                memoryChunk.Ref.Allocate(
                source, typeIndex, parent);
        }

        /// <summary>
        /// Creates a new data array in the task graph.
        /// </summary>
        /// <remarks>
        /// The new data array will be created as a child of the parent node passed as argument.
        /// Newly created nodes will be valid for the current frame but have to
        /// get ticked during subsequent frames in order for them to stay alive.
        /// </remarks>
        /// <param name="source">Native list which will be copied into the newly created data array.</param>
        /// <param name="parent">Memory identifier of the parent for the new node.</param>
        /// <returns>Memory identifier that corresponds to the newly created node.</returns>
        public MemoryIdentifier Allocate<T>(NativeList<T> source, MemoryIdentifier parent) where T : struct
        {
            Assert.IsTrue(parent.IsValid);
            Assert.IsTrue(source.Length <= short.MaxValue);

            var typeIndex = GetDataTypeIndex<T>();

            Assert.IsTrue(typeIndex.IsValid);

            return
                memoryChunk.Ref.Allocate(
                source, typeIndex, parent);
        }

        /// <summary>
        /// Marks an individual task graph node for deletion during the next update cycle.
        /// </summary>
        /// <remarks>
        /// This method can be used in cases where tasks need to replace individual
        /// nodes of the task graph section. The node to be replaced can be marked
        /// for deletion and a new node can be created that replaces the old one.
        /// </remarks>
        /// <param name="identifier">Identifier of the node to be deleted.</param>
        public void MarkForDelete(MemoryIdentifier identifier)
        {
            memoryChunk.Ref.MarkForDelete(identifier);
        }

        /// <summary>
        /// Introduces a new semantic query expression.
        /// </summary>
        /// <seealso cref="Unity.Kinematica.Query"/>
        public Query Query
        {
            get => Query.Create(ref Binary);
        }

        /// <summary>
        /// Pushes a new sampling time into the pose stream.
        /// </summary>
        /// <remarks>
        /// This method accepts a query result (most likely obtained from
        /// a semantic query). It unconditionally extracts the first pose
        /// from the pose sequence and forwards it as the next sampling time
        /// to the internal pose stream generator.
        /// </remarks>
        /// <param name="queryResult">Query result obtained from a semantic query.</param>
        /// <seealso cref="Query"/>
        public void Push(QueryResult queryResult)
        {
            if (queryResult.length > 0)
            {
                ref var binary = ref Binary;

                ref var interval = ref binary.GetInterval(
                    queryResult[0].intervalIndex);

                Push(interval.GetTimeIndex(interval.firstFrame));
            }

            queryResult.Dispose();
        }

        /// <summary>
        /// Reorders a task graph node to be the first child of its parent.
        /// </summary>
        /// <remarks>
        /// Newly created task graph nodes will be created as the last child
        /// of its parent. Subsequently, the new node will be executed after
        /// its siblings during the execution of the task graph. This method
        /// allows to override this default beahavior and allow the newly
        /// created node to be executed before its siblings.
        /// </remarks>
        /// <param name="identifier">Identifier of the node that should be reordered.</param>
        public void BringToFront(MemoryIdentifier identifier)
        {
            memoryChunk.Ref.BringToFront(identifier);
        }

        internal AffineTransform GetLocalTransform(int i)
        {
            return LocalSpaceTransformBuffer[i];
        }

        internal int TransformCount => poseGenerator.LocalSpaceTransformBuffer.Length;

        internal TransformBuffer LocalSpaceTransformBuffer => poseGenerator.LocalSpaceTransformBuffer;
    }
}
