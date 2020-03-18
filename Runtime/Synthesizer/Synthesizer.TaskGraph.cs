using System;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using UnityEngine.Assertions;

namespace Unity.Kinematica
{
    public partial struct MotionSynthesizer
    {
        void UpdateTaskGraph()
        {
            //
            // Calling ExecuteTasks() will mutate the internal state.
            // In order to be able to rewind the internal state to
            // its state during debugging we copy the memory chunk
            // to a shadow buffer. This is necessary since we will
            // append the memory chunk buffer to the snapshot during
            // OnPostProcess() which is called in a PostLateUpdate callback.
            //

            ref var memoryChunk = ref this.memoryChunk.Ref;

#if UNITY_EDITOR
            if (immutable)
            {
                memoryChunk.CopyFrom(
                    ref memoryChunkShadow.Ref);
            }
            else
            {
                memoryChunkShadow.Ref.CopyFrom(
                    ref memoryChunk);
            }
#endif
            PreExecuteTasks(ref memoryChunk);

            CopyOverrides();

            memoryChunk.SweepAndPrune(sizeOfTable);

            Execute(memoryChunk.Root);
        }

        unsafe void PreExecuteTasks(ref MemoryChunk memoryChunk)
        {
            var node = memoryChunk.Root;

            while (node.IsValid)
            {
                var header = memoryChunk.GetHeader(node);

                if (header->IsDirty)
                {
                    header->ClearDirty();

                    var typeFlag = dataTypes[header->typeIndex].flag;

                    if (typeFlag == DataType.Flag.TopologySort)
                    {
                        TopologySort(ref memoryChunk, header->self);
                    }
                }

                header->ClearSucceeded();

                node = memoryChunk.Next(node);
            }
        }

        struct Node
        {
            public MemoryIdentifier identifier;

            public int index;
            public int count;

            public static Node Create(MemoryIdentifier identifier)
            {
                return new Node
                {
                    identifier = identifier
                };
            }
        }

        struct Edge
        {
            public MemoryIdentifier from;
            public MemoryIdentifier to;

            public static Edge Create(MemoryIdentifier from, MemoryIdentifier to)
            {
                return new Edge
                {
                    from = from,
                    to = to
                };
            }
        }

        unsafe void TopologySort(ref MemoryChunk memoryChunk, MemoryIdentifier parent)
        {
            Assert.IsTrue(parent.IsValid);

            var child = memoryChunk.FirstChild(parent);

            if (child.IsValid)
            {
                var nodes = new NativeList<Node>(32, Allocator.Temp);

                var edges = new NativeList<Edge>(32, Allocator.Temp);

                //
                // Collect all nodes under parent and their edges.
                //

                while (child.IsValid)
                {
                    var header = memoryChunk.GetHeader(child);

                    var payload = (byte*)(header + 1);

                    var typeIndex = header->typeIndex;

                    ref var dataType = ref dataTypes[typeIndex];

                    for (int i = 0; i < dataType.numInputFields; ++i)
                    {
                        var field = dataFields[dataType.inputFieldIndex + i];

                        var fieldPtr =
                            (MemoryIdentifier*)(
                                payload + field.fieldOffset);

                        edges.Add(Edge.Create(child, *fieldPtr));
                    }

                    for (int i = 0; i < dataType.numOutputFields; ++i)
                    {
                        var field = dataFields[dataType.outputFieldIndex + i];

                        var fieldPtr =
                            (MemoryIdentifier*)(
                                payload + field.fieldOffset);

                        edges.Add(Edge.Create(*fieldPtr, child));
                    }

                    nodes.Add(Node.Create(child));

                    child = memoryChunk.NextSibling(child);
                }

                //
                // Organize edges such that each node contains an array of incoming edges.
                //

                int numNodes = nodes.Length;

                int numEdges = edges.Length;

                var incoming = new NativeList<MemoryIdentifier>(numEdges, Allocator.Temp);

                var nodePtr = (Node*)
                    NativeListUnsafeUtility.GetUnsafePtr(nodes);

                for (int i = 0; i < numNodes; ++i)
                {
                    var node = nodePtr[i].identifier;

                    nodePtr[i].index = incoming.Length;

                    for (int j = 0; j < numEdges; ++j)
                    {
                        if (edges[j].to == node)
                        {
                            incoming.Add(edges[j].from);

                            nodePtr[i].count++;
                        }
                    }
                }

                //
                // Sort nodes such that for every edge uv from vertex u to vertex v, u comes before v.
                //

                var sortedNodes =
                    TopologicalSort(nodes, incoming);

                Assert.IsTrue(sortedNodes.Length == numNodes);

                for (int i = 0; i < numNodes; ++i)
                {
                    if (nodes[i].count > 0)
                    {
                        throw new InvalidOperationException(
                            $"Graph for shader node {parent.index} does not form a directed acyclic graph (DAG).");
                    }
                }

                //
                // Organize all children under parent according to the sort result into a flat list.
                //

                var nodeIndex = numNodes - 1;

                memoryChunk.GetHeader(parent)->firstChild = sortedNodes[nodeIndex];

                var previous = memoryChunk.GetHeader(sortedNodes[nodeIndex]);

                Assert.IsTrue(previous->self == sortedNodes[nodeIndex]);

                previous->parent = parent;

                previous->firstChild = MemoryIdentifier.Invalid;
                previous->previousSibling = MemoryIdentifier.Invalid;
                previous->nextSibling = MemoryIdentifier.Invalid;

                while (--nodeIndex >= 0)
                {
                    var current = memoryChunk.GetHeader(sortedNodes[nodeIndex]);

                    Assert.IsTrue(current->self == sortedNodes[nodeIndex]);

                    current->parent = parent;

                    current->firstChild = MemoryIdentifier.Invalid;

                    current->previousSibling = sortedNodes[nodeIndex + 1];
                    current->nextSibling = MemoryIdentifier.Invalid;

                    previous->nextSibling = sortedNodes[nodeIndex];

                    previous = current;
                }

                sortedNodes.Dispose();
                edges.Dispose();
                nodes.Dispose();
                incoming.Dispose();
            }
        }

        /// <summary>
        /// Topological Sorting (Kahn's algorithm)
        /// </summary>
        /// <remarks>https://en.wikipedia.org/wiki/Topological_sorting</remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="nodes">All nodes of directed acyclic graph.</param>
        /// <param name="edges">All edges of directed acyclic graph.</param>
        /// <returns>Sorted node in topological order.</returns>
        static unsafe NativeList<MemoryIdentifier> TopologicalSort(NativeList<Node> nodes, NativeList<MemoryIdentifier> edges)
        {
            // Empty list that will contain the sorted elements
            var L = new NativeList<MemoryIdentifier>(nodes.Length, Allocator.Temp);

            // Set of all nodes with no incoming edges
            var S = new NativeList<MemoryIdentifier>(32, Allocator.Temp);

            int numNodes = nodes.Length;

            var nodePtr = (Node*)
                NativeListUnsafeUtility.GetUnsafePtr(nodes);

            for (int i = 0; i < numNodes; ++i)
            {
                if (nodePtr[i].count <= 0)
                {
                    S.Add(nodePtr[i].identifier);
                }
            }

            // while S is non-empty do
            while (S.Length > 0)
            {
                // remove a node n from S
                var n = S[S.Length - 1];
                S.RemoveAtSwapBack(S.Length - 1);

                // add n to tail of L
                L.Add(n);

                // for each node m with an edge e from n to m do
                for (int m = 0; m < numNodes; ++m)
                {
                    var index = nodePtr[m].index;

                    for (int e = 0; e < nodePtr[m].count; ++e)
                    {
                        if (edges[index + e] == n)
                        {
                            // remove edge e from the graph
                            edges[index + e] =
                                edges[--nodePtr[m].count + index];

                            // if m has no other incoming edges then
                            if (nodePtr[m].count <= 0)
                            {
                                // insert m into S
                                S.Add(nodePtr[m].identifier);
                            }
                        }
                    }
                }
            }

            S.Dispose();

            return L;
        }

        internal void CopyOverrides()
        {
            ref var memoryChunk = ref this.memoryChunk.Ref;

#if UNITY_EDITOR
            if (immutable)
            {
                ref var memoryChunkShadow =
                    ref this.memoryChunkShadow.Ref;

                memoryChunkShadow.CopyOverridesTo(
                    ref memoryChunk, sizeOfTable);
            }
#endif
        }

        internal unsafe Result Execute(MemoryIdentifier identifier)
        {
            ref var memoryChunk = ref this.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(identifier);

            var result = Result.Success;

            var executeFunction =
                dataTypes[header->typeIndex].executeFunction;

            if (executeFunction.IsValid)
            {
                var payloadPtr = memoryChunk.GetPayload(identifier);

                result = executeFunction.Invoke(payloadPtr);
            }

            if (result != Result.Failure)
            {
                header->SetSucceeded();
            }

            return result;
        }
    }
}
