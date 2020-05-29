using System;
using System.Collections.Generic;

using UnityEngine.UIElements;
using UnityEngine.Assertions;

using UnityEditor;
using UnityEditor.Experimental.GraphView;

using Unity.SnapshotDebugger;
using Unity.Mathematics;

namespace Unity.Kinematica.Editor
{
    internal class GraphNodeView : GraphView
    {
        internal EditorWindow window;

        internal class LayoutElement
        {
            public GraphElement graphElement;

            public List<LayoutElement> children = new List<LayoutElement>();

            public static LayoutElement Create(GraphElement graphElement)
            {
                return new LayoutElement
                {
                    graphElement = graphElement
                };
            }

            public float UpdateAccumulatedHeight(float verticalSpacing)
            {
                var height = math.max(0, children.Count - 1) * verticalSpacing;

                foreach (var childElement in children)
                {
                    height +=
                        childElement.UpdateAccumulatedHeight(
                            verticalSpacing);
                }

                return math.max(height,
                    graphElement.layout.height);
            }

            public float UpdateLayout(LayoutElement parent, float layerWidth, float2 position, float2 spacing)
            {
                var selfHeight = UpdateLayout(
                    position, layerWidth, spacing.y);

                var nextLayerWidth = GetMaximumWidth();

                position.x += layerWidth + spacing.x;

                foreach (var child in children)
                {
                    var height = child.UpdateLayout(
                        this, nextLayerWidth,
                        position, spacing);

                    position.y += height + spacing.y;
                }

                return selfHeight;
            }

            float UpdateLayout(float2 position, float layerWidth, float verticalSpacing)
            {
                var accumulatedHeight =
                    UpdateAccumulatedHeight(verticalSpacing);

                position.y +=
                    (accumulatedHeight - graphElement.layout.height) * 0.5f;

                var graphGroup = graphElement as GraphNodeGroup;

                if (graphGroup != null)
                {
                    position.x += graphGroup.containedElementsRect.position.x;
                    position.y += graphGroup.containedElementsRect.position.y;

                    graphGroup.GeometryChangededCallback(position);
                }
                else
                {
                    position.x +=
                        (layerWidth - graphElement.layout.width) * 0.5f;

                    var nodePosition = graphElement.GetPosition();
                    nodePosition.position = position;
                    graphElement.SetPosition(nodePosition);
                }

                return accumulatedHeight;
            }

            public float GetMaximumWidth()
            {
                float maximumWidth = 0.0f;

                foreach (var child in children)
                {
                    var graphElement = child.graphElement;

                    float nodeWidth = graphElement.layout.width;

                    if (nodeWidth > maximumWidth)
                    {
                        maximumWidth = nodeWidth;
                    }
                }

                return maximumWidth;
            }

            public LayoutElement Find(GraphElement graphElement)
            {
                if (this.graphElement == graphElement)
                {
                    return this;
                }

                foreach (var child in children)
                {
                    var layoutElement = child.Find(graphElement);

                    if (layoutElement != null)
                    {
                        return layoutElement;
                    }
                }

                return null;
            }

            public LayoutElement AddToLayout(GraphElement graphElement)
            {
                var element =
                    Create(graphElement);

                children.Add(element);

                return element;
            }
        }

        LayoutElement rootElement;

        List<GraphNodeGroup> graphGroups = new List<GraphNodeGroup>();

        HashSet<GraphNode> graphNodes = new HashSet<GraphNode>();

        HashSet<GraphNodeEdge> graphEdges = new HashSet<GraphNodeEdge>();

        HashSet<GraphNode> graphNodesCache;

        HashSet<GraphNodeGroup> graphGroupsCache;

        HashSet<GraphNodeEdge> graphEdgesCache;

        internal MemoryRef<MemoryChunk> memoryChunk;

        int updateDelay;
        int version;

        internal GraphNodeView(EditorWindow window)
        {
            this.window = window;

            version = -1;

            memoryChunk = MemoryRef<MemoryChunk>.Null;

            InitializeManipulators();

            SetupZoom(0.05f, 2f);

            this.StretchToParentSize();
        }

        bool RequiresUpdate(MemoryRef<MemoryChunk> memoryChunk)
        {
            if (memoryChunk.IsValid)
            {
                if (memoryChunk.Ref.Version == version)
                {
                    return false;
                }

                version = memoryChunk.Ref.Version;
                return true;
            }

            version = -1;

            return true;
        }

        internal void Update(MemoryRef<MemoryChunk> memoryChunk)
        {
            this.memoryChunk = memoryChunk;

            if (Debugger.instance.rewind || ReadyForUpdate())
            {
                if (RequiresUpdate(memoryChunk))
                {
                    UpdateLayout();
                }
            }

            if (!memoryChunk.IsValid || version != memoryChunk.Ref.Version)
            {
                return;
            }

            foreach (var graphNode in graphNodes)
            {
                graphNode.UpdateState();
            }

            foreach (var graphGroup in graphGroups)
            {
                graphGroup.Update();
            }
        }

        bool ReadyForUpdate()
        {
            if (updateDelay > 0)
            {
                updateDelay--;

                return false;
            }

            if (!HasValidLayout())
            {
                updateDelay = 2;

                return false;
            }

            return true;
        }

        bool HasValidLayout()
        {
            foreach (var graphGroup in graphGroups)
            {
                if (!graphGroup.HasValidLayout())
                {
                    return false;
                }
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

        void UpdateLayout(float2 spacing)
        {
            if (rootElement != null)
            {
                foreach (var child in rootElement.children)
                {
                    child.UpdateLayout(rootElement,
                        rootElement.GetMaximumWidth(), 0.0f, spacing);
                }
            }
        }

        internal void GeometryChangededCallback()
        {
            const float horizontalSpacing = 100.0f;
            const float verticalSpacing = 50.0f;

            UpdateLayout(new float2(
                horizontalSpacing, verticalSpacing));
        }

        unsafe void UpdateLayout()
        {
            //
            // Layout prologue
            //

            rootElement = null;

            graphNodesCache = new HashSet<GraphNode>(graphNodes);
            graphGroupsCache = new HashSet<GraphNodeGroup>(graphGroups);
            graphEdgesCache = new HashSet<GraphNodeEdge>(graphEdges);

            graphNodes.Clear();
            graphGroups.Clear();
            graphEdges.Clear();

            //
            // Create graph nodes
            //

            if (memoryChunk.IsValid)
            {
                rootElement = InitializeNodes();
            }

            //
            // Layout epilogue
            //

            foreach (var graphNode in graphNodesCache)
            {
                RemoveElement(graphNode);
            }

            foreach (var graphGroup in graphGroupsCache)
            {
                var graphNodes = graphGroup.RemoveAll();

                foreach (var graphNode in graphNodes)
                {
                    RemoveElement(graphNode);
                }

                RemoveElement(graphGroup);
            }

            foreach (var graphEdge in graphEdgesCache)
            {
                RemoveElement(graphEdge);
            }

            graphNodesCache = null;
            graphGroupsCache = null;
            graphEdgesCache = null;
        }

        unsafe LayoutElement InitializeNodes()
        {
            ref var memoryChunk = ref this.memoryChunk.Ref;

            var layoutElement = LayoutElement.Create(null);

            InitializeNode(layoutElement, memoryChunk.Root, 0);

            return layoutElement;
        }

        unsafe GraphNodePort InitializeNode(LayoutElement parentElement, MemoryIdentifier self, int layerIndex)
        {
            Assert.IsTrue(self.IsValid);

            ref var memoryChunk = ref this.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(self);

            var typeIndex = header->typeIndex;

            Assert.IsTrue(header->self == self);

            Assert.IsTrue(typeIndex.IsValid);

            var dataType = DataType.Types[typeIndex];

            if (DataAttribute.Flag(dataType.type) == DataType.Flag.TopologySort)
            {
                if (!GraphNodeGroup.IsValidGroup(ref memoryChunk, self))
                {
                    return null;
                }

                var group = GetGroupFromCache(self);

                if (group == null)
                {
                    group = GraphNodeGroup.Create(this, self);

                    Assert.IsTrue(group != null);

                    AddElement(group);
                }

                Assert.IsTrue(group != null);

                graphGroups.Add(group);

                parentElement.AddToLayout(group);

                group.UpdateLayout();

                return group.inputPort;
            }
            else
            {
                var type = dataType.type;

                var graphNode = GetNodeFromCache(type, self);

                if (graphNode == null)
                {
                    graphNode =
                        GraphNode.Create(
                            this, type, self);

                    AddElement(graphNode);
                }

                Assert.IsTrue(graphNode != null);

                graphNodes.Add(graphNode);

                var layoutElement =
                    parentElement.AddToLayout(graphNode);

                var child = memoryChunk.FirstChild(self);

                var selfPort = graphNode.GetReadPort();

                while (child.IsValid)
                {
                    var childPort =
                        InitializeNode(layoutElement,
                            child, layerIndex + 1);

                    if (childPort != null)
                    {
                        var edge = GetEdgeFromCache(childPort, selfPort);

                        if (edge == null)
                        {
                            edge = new GraphNodeEdge()
                            {
                                input = childPort,
                                output = selfPort
                            };

                            Connect(edge);
                        }

                        graphEdges.Add(edge);

                        Assert.IsTrue(childPort.owner != null);
                    }

                    child = memoryChunk.NextSibling(child);
                }

                return graphNode.GetWritePort();
            }
        }

        bool Connect(GraphNodeEdge edge)
        {
            if (edge.input == null || edge.output == null)
                return false;

            var inputPort = edge.input as GraphNodePort;
            var outputPort = edge.output as GraphNodePort;
            var inputNode = inputPort.node as GraphNode;
            var outputNode = outputPort.node as GraphNode;

            AddElement(edge);

            edge.input.Connect(edge);
            edge.output.Connect(edge);

            if (inputNode != null)
            {
                inputNode.RefreshPorts();
            }

            if (outputNode != null)
            {
                outputNode.RefreshPorts();
            }

            edge.isConnected = true;

            return true;
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

        GraphNode GetNodeFromCache(Type type, MemoryIdentifier identifier)
        {
            Assert.IsTrue(graphNodesCache != null);

            foreach (var node in graphNodesCache)
            {
                if (node.type == type)
                {
                    if (node.identifier == identifier)
                    {
                        graphNodesCache.Remove(node);

                        return node;
                    }
                }
            }

            return null;
        }

        GraphNodeGroup GetGroupFromCache(MemoryIdentifier identifier)
        {
            Assert.IsTrue(graphGroupsCache != null);

            foreach (var group in graphGroupsCache)
            {
                if (group.identifier == identifier)
                {
                    graphGroupsCache.Remove(group);

                    return group;
                }
            }

            return null;
        }

        protected virtual void InitializeManipulators()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ClickSelector());
        }
    }
}
