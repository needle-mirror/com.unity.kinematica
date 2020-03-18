using System;
using System.Collections.Generic;
using Unity.Kinematica.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

using UnityEditor.Experimental.GraphView;

using Unity.Mathematics;
using Unity.SnapshotDebugger;

namespace Unity.Kinematica.Editor
{
    internal class GraphNodeGroup : Group
    {
        internal MemoryIdentifier identifier { get; private set; }

        GraphNodeView owner;

        internal struct LayoutIndex
        {
            public int layerIndex;
            public int groupIndex;
            public int elementIndex;

            public static LayoutIndex Create(int layerIndex, int groupIndex, int elementIndex)
            {
                return new LayoutIndex
                {
                    layerIndex = layerIndex,
                    groupIndex = groupIndex,
                    elementIndex = elementIndex
                };
            }

            public static LayoutIndex Default => Create(0, 0, 0);
        }

        Dictionary<GraphNode, LayoutIndex> layoutIndices = new Dictionary<GraphNode, LayoutIndex>();

        internal class Layer
        {
            public class Group
            {
                public class Element
                {
                    public GraphNode graphNode;

                    public static Element Create(GraphNode graphNode)
                    {
                        return new Element
                        {
                            graphNode = graphNode
                        };
                    }
                }

                public float height;
                public float verticalPosition;
                public List<Element> elements = new List<Element>();

                public static Group Create()
                {
                    return new Group();
                }

                public LayoutIndex AddToLayout(GraphNode graphNode)
                {
                    var layoutIndex =
                        LayoutIndex.Create(-1, -1, elements.Count);

                    elements.Add(Element.Create(graphNode));

                    return layoutIndex;
                }

                public float UpdateHeight(float verticalSpacing)
                {
                    var numElementsMinusOne = elements.Count - 1;

                    height = verticalSpacing * numElementsMinusOne;

                    foreach (var element in elements)
                    {
                        var graphNode = element.graphNode;

                        height += graphNode.layout.height;
                    }

                    return height;
                }
            }

            public float width;

            public List<Group> groups = new List<Group>();

            public void UpdateWidth()
            {
                width = 0.0f;

                foreach (var group in groups)
                {
                    foreach (var element in group.elements)
                    {
                        var graphNode = element.graphNode;

                        float nodeWidth = graphNode.layout.width;

                        if (nodeWidth > width)
                        {
                            width = nodeWidth;
                        }
                    }
                }
            }

            public LayoutIndex AddToLayout(GraphNode graphNode, int groupIndex)
            {
                while (groups.Count <= groupIndex)
                {
                    groups.Add(Group.Create());
                }

                var layoutIndex =
                    groups[groupIndex].AddToLayout(graphNode);

                layoutIndex.groupIndex = groupIndex;

                return layoutIndex;
            }

            public static Layer Create()
            {
                return new Layer();
            }
        }

        List<Layer> layers = new List<Layer>();

        HashSet<GraphNode> graphNodes = new HashSet<GraphNode>();

        HashSet<GraphNodeEdge> graphEdges = new HashSet<GraphNodeEdge>();

        HashSet<GraphNode> graphNodesCache;

        HashSet<GraphNodeEdge> graphEdgesCache;

        internal GraphNodePort inputPort;

        readonly string styleSheet = "GraphNodeGroup.uss";

        GraphNodeGroup(GraphNodeView graphView, MemoryIdentifier identifier)
        {
            owner = graphView;

            this.identifier = identifier;

            UIElementsUtils.ApplyStyleSheet(styleSheet, this);

            RegisterCallback<GeometryChangedEvent>(GeometryChangededCallback);

            inputPort =
                GraphNodePort.Create(
                    Direction.Input, typeof(GraphNode), null);

            inputPort.Initialize(this, string.Empty);

            headerContainer.Add(inputPort);
        }

        public override bool IsSelectable()
        {
            return false;
        }

        internal static GraphNodeGroup Create(GraphNodeView graphView, MemoryIdentifier identifier)
        {
            return new GraphNodeGroup(graphView, identifier);
        }

        internal bool HasValidLayout()
        {
            if (float.IsNaN(layout.width) || float.IsNaN(layout.height))
            {
                return false;
            }

            foreach (var graphNode in graphNodes)
            {
                if (float.IsNaN(graphNode.layout.width) || float.IsNaN(graphNode.layout.height))
                {
                    return false;
                }
            }

            return true;
        }

        ref MemoryChunk GetMemoryChunk()
        {
            return ref owner.memoryChunk.Ref;
        }

        int GetNumGroupsInLayer(int layerIndex)
        {
            if (layerIndex < layers.Count)
            {
                return layers[layerIndex].groups.Count;
            }

            return 0;
        }

        LayoutIndex AddToLayout(GraphNode graphNode, int layerIndex, int groupIndex)
        {
            while (layers.Count <= layerIndex)
            {
                layers.Add(Layer.Create());
            }

            var layoutIndex =
                layers[layerIndex].AddToLayout(
                    graphNode, groupIndex);

            layoutIndex.layerIndex = layerIndex;

            return layoutIndex;
        }

        internal void Update()
        {
            foreach (var graphNode in graphNodes)
            {
                graphNode.UpdateState();
            }
        }

        void UpdateLayout(Vector2 offsetPosition, float horizontalSpacing, float verticalSpacing)
        {
            int maxNumGroups = 0;

            foreach (var layer in layers)
            {
                if (layer.groups.Count > maxNumGroups)
                {
                    maxNumGroups = layer.groups.Count;
                }
            }

            var groupHeights = new float[maxNumGroups];

            foreach (var layer in layers)
            {
                layer.UpdateWidth();

                int numGroups = layer.groups.Count;

                for (int i = 0; i < numGroups; i++)
                {
                    var group = layer.groups[i];

                    var height =
                        group.UpdateHeight(
                            verticalSpacing);

                    if (height > groupHeights[i])
                    {
                        groupHeights[i] = height;
                    }
                }
            }

            var groupPositions = new float[maxNumGroups];

            float verticalGroupPosition = 0.0f;

            for (int i = 0; i < maxNumGroups; ++i)
            {
                groupPositions[i] = verticalGroupPosition;

                verticalGroupPosition +=
                    groupHeights[i] + verticalSpacing;
            }

            float currentHorizontal = 0.0f;

            var numLayers = layers.Count;

            for (int i = 0; i < numLayers; ++i)
            {
                var layer = layers[i];

                int numGroups = layer.groups.Count;

                for (int j = 0; j < numGroups; j++)
                {
                    var group = layer.groups[j];

                    var currentVertical =
                        groupPositions[j];

                    Assert.IsTrue(groupHeights[j] >= group.height);

                    var groupOffset =
                        (groupHeights[j] - group.height) * 0.5f;

                    currentVertical += groupOffset;

                    int numElements = group.elements.Count;

                    for (int k = 0; k < numElements; ++k)
                    {
                        var element = group.elements[k];

                        var graphNode = element.graphNode;

                        var position = offsetPosition;

                        position.x += currentHorizontal;
                        position.y += currentVertical;

                        currentVertical +=
                            graphNode.layout.height + verticalSpacing;

                        var nodePosition = graphNode.GetPosition();
                        nodePosition.position = position;
                        graphNode.SetPosition(nodePosition);
                    }
                }

                currentHorizontal +=
                    layers[i].width + horizontalSpacing;
            }
        }

        internal void UpdateLayout()
        {
            //
            // Layout prologue
            //

            layers.Clear();
            layoutIndices.Clear();

            graphNodesCache = new HashSet<GraphNode>(graphNodes);
            graphEdgesCache = new HashSet<GraphNodeEdge>(graphEdges);

            graphNodes.Clear();
            graphEdges.Clear();

            //
            // Create graph node for this group
            //

            InitializeNodes();

            //
            // Create graph edges for this group
            //

            InitializeEdges();

            //
            // Layout epilogue
            //

            foreach (var graphEdge in graphEdgesCache)
            {
                owner.RemoveElement(graphEdge);
            }

            foreach (var graphNode in graphNodesCache)
            {
                RemoveElement(graphNode);
            }

            graphNodesCache = null;
            graphEdgesCache = null;
        }

        internal void GeometryChangededCallback(Vector2 offsetPosition)
        {
            const float horizontalSpacing = 100.0f;
            const float verticalSpacing = 50.0f;

            UpdateLayout(offsetPosition,
                horizontalSpacing, verticalSpacing);

            foreach (var graphNode in graphNodes)
            {
                if (!ContainsElement(graphNode))
                {
                    AddElement(graphNode);
                }
            }

            UpdateGeometryFromContent();
        }

        internal GraphNode[] RemoveAll()
        {
            RemoveAllEdges();
            return RemoveAllNodes();
        }

        void GeometryChangededCallback(GeometryChangedEvent e)
        {
            if (math.abs(e.oldRect.width - e.newRect.width) <= 10)
                return;

            if (math.abs(e.oldRect.height - e.newRect.height) <= 10)
                return;

            owner.GeometryChangededCallback();
        }

        public static unsafe bool IsValidGroup(ref MemoryChunk memoryChunk, MemoryIdentifier identifier)
        {
            var current = memoryChunk.FirstChild(identifier);

            while (current.IsValid)
            {
                var header = memoryChunk.GetHeader(current);

                var typeIndex = header->typeIndex;

                Assert.IsTrue(header->self == current);

                Assert.IsTrue(typeIndex.IsValid);

                var dataType = DataType.Types[typeIndex];

                if (dataType.executable)
                {
                    return true;
                }

                current = memoryChunk.NextSibling(current);
            }

            return false;
        }

        unsafe void InitializeNodes()
        {
            ref var memoryChunk = ref GetMemoryChunk();

            var current = memoryChunk.FirstChild(identifier);

            while (current.IsValid)
            {
                var header = memoryChunk.GetHeader(current);

                var typeIndex = header->typeIndex;

                Assert.IsTrue(header->self == current);

                Assert.IsTrue(typeIndex.IsValid);

                var dataType = DataType.Types[typeIndex];

                if (dataType.executable)
                {
                    var layoutIndex =
                        CreateInputFields(current);

                    var layerIndex = layoutIndex.layerIndex;
                    var groupIndex = layoutIndex.groupIndex;

                    var type = dataType.type;

                    var graphNode = GetNodeFromCache(type, current);

                    if (graphNode == null)
                    {
                        graphNode =
                            GraphNode.Create(
                                owner, type, current);

                        owner.AddElement(graphNode);
                    }

                    Assert.IsTrue(graphNode != null);

                    graphNodes.Add(graphNode);

                    layoutIndices[graphNode] =
                        AddToLayout(graphNode,
                            layerIndex, groupIndex);

                    CreateOutputFields(current);
                }

                current = memoryChunk.NextSibling(current);
            }
        }

        unsafe static int GetArrayLength(ref MemoryChunk memoryChunk, MemoryIdentifier identifier)
        {
            var header = memoryChunk.GetHeader(identifier);

            var typeIndex = header->typeIndex;

            Assert.IsTrue(header->self == identifier);

            Assert.IsTrue(typeIndex.IsValid);

            var type = DataType.Types[typeIndex].type;

            if (DataAttribute.Flag(type) == DataType.Flag.ExpandArray)
            {
                return header->length;
            }

            return 1;
        }

        unsafe LayoutIndex CreateInputFields(MemoryIdentifier identifier)
        {
            ref var memoryChunk = ref GetMemoryChunk();

            var header = memoryChunk.GetHeader(identifier);

            var payload = (byte*)(header + 1);

            var typeIndex = header->typeIndex;

            Assert.IsTrue(header->self == identifier);

            Assert.IsTrue(typeIndex.IsValid);

            var dataType = DataType.Types[typeIndex];

            var fieldLayoutIndex =
                CalculateLayoutIndex(identifier);

            var layerIndex = fieldLayoutIndex.layerIndex;
            var groupIndex = fieldLayoutIndex.groupIndex;

            var numInputFields = 0;

            foreach (var field in dataType.inputFields)
            {
                var target =
                    *(MemoryIdentifier*)(
                        payload + field.offset);

                if (target.IsValid)
                {
                    numInputFields++;

                    var numNodes =
                        GetArrayLength(
                            ref memoryChunk, target);

                    for (int i = 0; i < numNodes; ++i)
                    {
                        var graphNode =
                            GetExistingNode(target, i);

                        if (graphNode == null)
                        {
                            graphNode = GetNodeFromCache(
                                field.type, target, i);

                            if (graphNode == null)
                            {
                                graphNode =
                                    GraphNode.Create(
                                        owner, field.type, target, i);

                                owner.AddElement(graphNode);
                            }

                            layoutIndices[graphNode] =
                                AddToLayout(graphNode,
                                    layerIndex, groupIndex);
                        }

                        Assert.IsTrue(graphNode != null);

                        graphNodes.Add(graphNode);
                    }
                }
            }

            var nodeLayerIndex = fieldLayoutIndex;

            if (numInputFields > 0)
            {
                nodeLayerIndex.layerIndex++;
            }

            return nodeLayerIndex;
        }

        LayoutIndex GetLayoutIndex(MemoryIdentifier identifier)
        {
            var graphNode = GetExistingNode(identifier);

            Assert.IsTrue(graphNode != null);
            Assert.IsTrue(layoutIndices.ContainsKey(graphNode));

            return layoutIndices[graphNode];
        }

        unsafe void CreateOutputFields(MemoryIdentifier identifier)
        {
            ref var memoryChunk = ref GetMemoryChunk();

            var header = memoryChunk.GetHeader(identifier);

            var payload = (byte*)(header + 1);

            var typeIndex = header->typeIndex;

            Assert.IsTrue(header->self == identifier);

            Assert.IsTrue(typeIndex.IsValid);

            var dataType = DataType.Types[typeIndex];

            var fieldLayoutIndex =
                GetLayoutIndex(identifier);

            var layerIndex = fieldLayoutIndex.layerIndex + 1;
            var groupIndex = fieldLayoutIndex.groupIndex;

            foreach (var field in dataType.outputFields)
            {
                var target =
                    *(MemoryIdentifier*)(
                        payload + field.offset);

                if (target.IsValid)
                {
                    var numNodes =
                        GetArrayLength(
                            ref memoryChunk, target);

                    for (int i = 0; i < numNodes; ++i)
                    {
                        var graphNode = GetNodeFromCache(
                            field.type, target, i);

                        if (graphNode == null)
                        {
                            graphNode =
                                GraphNode.Create(
                                    owner, field.type, target);

                            owner.AddElement(graphNode);
                        }

                        Assert.IsTrue(graphNode != null);

                        layoutIndices[graphNode] =
                            AddToLayout(graphNode,
                                layerIndex, groupIndex);

                        graphNodes.Add(graphNode);
                    }
                }
            }
        }

        unsafe LayoutIndex CalculateLayoutIndex(MemoryIdentifier identifier)
        {
            ref var memoryChunk = ref GetMemoryChunk();

            var header = memoryChunk.GetHeader(identifier);

            var payload = (byte*)(header + 1);

            var typeIndex = header->typeIndex;

            Assert.IsTrue(header->self == identifier);

            Assert.IsTrue(typeIndex.IsValid);

            var dataType = DataType.Types[typeIndex];

            var layoutIndex = LayoutIndex.Default;

            layoutIndex.groupIndex =
                GetNumGroupsInLayer(
                    layoutIndex.layerIndex);

            foreach (var field in dataType.inputFields)
            {
                var target =
                    *(MemoryIdentifier*)(
                        payload + field.offset);

                if (target.IsValid)
                {
                    var graphNode = GetExistingNode(target);

                    if (graphNode != null)
                    {
                        Assert.IsTrue(layoutIndices.ContainsKey(graphNode));

                        var index = layoutIndices[graphNode];

                        if (index.layerIndex > layoutIndex.layerIndex)
                        {
                            layoutIndex = index;
                        }
                    }
                }
            }

            return layoutIndex;
        }

        void InitializeEdges()
        {
            foreach (var node in graphNodes)
            {
                foreach (var input in node.inputPorts)
                {
                    Assert.IsTrue(input.field != null);

                    var identifier = node.GetPortValue(input);

                    foreach (var target in graphNodes)
                    {
                        if (target.identifier == identifier)
                        {
                            var edge = GetEdgeFromCache(input, target.GetReadPort());

                            if (edge == null)
                            {
                                edge = new GraphNodeEdge()
                                {
                                    input = input,
                                    output = target.GetReadPort()
                                };

                                Connect(edge);
                            }

                            graphEdges.Add(edge);
                        }
                    }
                }

                foreach (var output in node.outputPorts)
                {
                    Assert.IsTrue(output.field != null);

                    var identifier = node.GetPortValue(output);

                    foreach (var target in graphNodes)
                    {
                        if (target.identifier == identifier)
                        {
                            var edge = GetEdgeFromCache(target.GetWritePort(), output);

                            if (edge == null)
                            {
                                edge = new GraphNodeEdge()
                                {
                                    input = target.GetWritePort(),
                                    output = output
                                };

                                Connect(edge);
                            }

                            graphEdges.Add(edge);
                        }
                    }
                }
            }
        }

        GraphNodeEdge GetEdgeFromCache(GraphNodePort from, GraphNodePort to)
        {
            Assert.IsTrue(graphEdgesCache != null);

            foreach (var edge in graphEdgesCache)
            {
                if (edge.input == from)
                {
                    if (edge.output == to)
                    {
                        graphEdgesCache.Remove(edge);

                        return edge;
                    }
                }
            }

            return null;
        }

        bool Connect(GraphNodeEdge edge)
        {
            if (edge.input == null || edge.output == null)
                return false;

            var inputPort = edge.input as GraphNodePort;
            var outputPort = edge.output as GraphNodePort;
            var inputNode = inputPort.node as GraphNode;
            var outputNode = outputPort.node as GraphNode;

            if (inputNode == null || outputNode == null)
            {
                return false;
            }

            owner.AddElement(edge);

            edge.input.Connect(edge);
            edge.output.Connect(edge);

            inputNode.RefreshPorts();
            outputNode.RefreshPorts();

            edge.isConnected = true;

            return true;
        }

        GraphNode GetNodeFromCache(Type type, MemoryIdentifier identifier, int arrayIndex = 0)
        {
            Assert.IsTrue(graphNodesCache != null);

            foreach (var node in graphNodesCache)
            {
                if (node.type == type)
                {
                    if (node.identifier == identifier)
                    {
                        if (node.arrayIndex == arrayIndex)
                        {
                            graphNodesCache.Remove(node);

                            return node;
                        }
                    }
                }
            }

            return null;
        }

        GraphNode GetExistingNode(MemoryIdentifier identifier, int arrayIndex = 0)
        {
            foreach (var node in graphNodes)
            {
                if (node.identifier == identifier)
                {
                    if (node.arrayIndex == arrayIndex)
                    {
                        return node;
                    }
                }
            }

            return null;
        }

        GraphNode[] RemoveAllNodes()
        {
            var result = new GraphNode[graphNodes.Count];

            int writeIndex = 0;

            foreach (var graphNode in graphNodes)
            {
                owner.AddElement(graphNode);

                result[writeIndex++] = graphNode;
            }

            graphNodes.Clear();

            return result;
        }

        void RemoveAllEdges()
        {
            foreach (var graphEdge in graphEdges)
            {
                owner.RemoveElement(graphEdge);
            }

            graphEdges.Clear();
        }
    }
}
