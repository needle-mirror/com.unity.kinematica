using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Assertions;

using UnityEditor.Experimental.GraphView;

using Unity.Collections.LowLevel.Unsafe;
using Unity.Kinematica.UIElements;
using Unity.Mathematics;

namespace Unity.Kinematica.Editor
{
    public class GraphNode : Node
    {
        internal List<GraphNodePort> inputPorts = new List<GraphNodePort>();
        internal List<GraphNodePort> outputPorts = new List<GraphNodePort>();

        internal GraphNodePort readPort;
        internal GraphNodePort writePort;

        protected VisualElement controlsContainer;
        //protected VisualElement debugContainer;

        VisualElement stateElement;

        public MemoryIdentifier identifier { get; private set; }

        public Type type { get; private set; }

        List<GraphNodePort> ports = new List<GraphNodePort>();

        internal GraphNodeView owner { private set; get; }

        public int arrayIndex { private set; get; }

        readonly string styleSheet = "GraphNode.uss";

        static MethodInfo fieldValueMethod;

        void Initialize(GraphNodeView owner, Type type, MemoryIdentifier identifier, int arrayIndex = 0)
        {
            if (fieldValueMethod == null)
            {
                fieldValueMethod = GetType().GetMethod(
                    nameof(GetFieldValueGeneric),
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsTrue(fieldValueMethod != null);
            }

            this.owner = owner;
            this.type = type;
            this.identifier = identifier;
            this.arrayIndex = arrayIndex;

            RegisterCallback<GeometryChangedEvent>(GeometryChangededCallback);

            UIElementsUtils.ApplyStyleSheet(styleSheet, this);

            InitializePorts();
            InitializeView();
            //InitializeDebug();

            DrawDefaultInspector();

            RefreshExpandedState();

            RefreshPorts();
        }

        public ref T Item<T>() where T : struct
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            return ref memoryChunk.GetRef<T>(identifier).Ref;
        }

        public MemoryRef<T> GetRef<T>(MemoryIdentifier identifier) where T : struct
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            return memoryChunk.GetRef<T>(identifier);
        }

        public MemoryArray<T> GetArray<T>(MemoryIdentifier identifier) where T : struct
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            return memoryChunk.GetArray<T>(identifier);
        }

        public void SetReadOnly(bool flag)
        {
            VisualElement selectionBorder =
                this.Q<VisualElement>("selection-border");

            VisualElement collapseButton =
                this.Q<VisualElement>("collapse-button");

            if (flag)
            {
                selectionBorder.style.display = DisplayStyle.None;
                collapseButton.style.display = DisplayStyle.None;
            }
            else
            {
                selectionBorder.style.display = DisplayStyle.Flex;
                collapseButton.style.display = DisplayStyle.Flex;
            }
        }

        public unsafe void UpdateState()
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(identifier);

            if (header->HasSucceeded)
            {
                stateElement.AddToClassList("success");
                stateElement.RemoveFromClassList("failure");
            }
            else
            {
                stateElement.AddToClassList("failure");
                stateElement.RemoveFromClassList("success");
            }
        }

        internal static GraphNode Create(GraphNodeView owner, Type type, MemoryIdentifier identifier, int arrayIndex = 0)
        {
            var nodeType = GraphNodeAttribute.GetType(type);

            if (nodeType != null)
            {
                var graphNode =
                    Activator.CreateInstance(nodeType) as GraphNode;

                if (graphNode == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to create node type {nodeType.FullName} for task type {type.FullName}.");
                }

                graphNode.Initialize(owner, type, identifier, arrayIndex);

                return graphNode;
            }
            else
            {
                var graphNode = new GraphNode();

                graphNode.Initialize(owner, type, identifier, arrayIndex);

                return graphNode;
            }
        }

        void GeometryChangededCallback(GeometryChangedEvent e)
        {
            if (math.abs(e.oldRect.width - e.newRect.width) <= 10)
                return;

            if (math.abs(e.oldRect.height - e.newRect.height) <= 10)
                return;

            owner.GeometryChangededCallback();
        }

        void InitializePorts()
        {
            var dataType = DataType.GetDataType(GetValueType());

            Assert.IsTrue(dataType != null);

            var inputFields = dataType.inputFields;

            foreach (var field in inputFields)
            {
                var name = field.name;
                var type = field.type;
                var info = field.info;

                var nodePort =
                    GraphNodePort.Create(
                        Direction.Input, type, info);

                inputPorts.Add(nodePort);
                inputContainer.Add(nodePort);

                nodePort.Initialize(this, name);
            }

            var outputFields = dataType.outputFields;

            foreach (var field in outputFields)
            {
                var name = field.name;
                var type = field.type;
                var info = field.info;

                var nodePort =
                    GraphNodePort.Create(
                        Direction.Output, type, info);

                outputPorts.Add(nodePort);
                outputContainer.Add(nodePort);

                nodePort.Initialize(this, name);
            }
        }

        internal GraphNodePort GetReadPort(Type type = null)
        {
            if (readPort == null)
            {
                if (type == null)
                {
                    type = typeof(MemoryIdentifier);
                }

                readPort =
                    GraphNodePort.Create(
                        Direction.Output, type);

                outputContainer.Add(readPort);

                readPort.Initialize(this, string.Empty);

                RefreshPorts();
            }

            return readPort;
        }

        internal GraphNodePort GetWritePort(Type type = null)
        {
            if (writePort == null)
            {
                if (type == null)
                {
                    type = typeof(MemoryIdentifier);
                }

                writePort =
                    GraphNodePort.Create(
                        Direction.Input, type);

                inputContainer.Add(writePort);

                writePort.Initialize(this, string.Empty);

                RefreshPorts();
            }

            return writePort;
        }

        void InitializeView()
        {
            controlsContainer = new VisualElement
            {
                name = "controls"
            };

            mainContainer.Add(controlsContainer);

            //debugContainer = new VisualElement
            //{
            //    name = "debug"
            //};

            //if (nodeTarget.debug)
            //  mainContainer.Add(debugContainer);

            var type = GetValueType();

            title = $"{DataAttribute.GetDescription(type)} [{identifier.index}]";

            var color = DataAttribute.GetColor(type);
            if (!color.Equals(Color.clear))
            {
                titleContainer.style.backgroundColor = color;
            }

            stateElement = new VisualElement { name = "state" };
            stateElement.Add(new VisualElement { name = "icon" });
            stateElement.style.flexDirection = FlexDirection.Row;

            titleContainer.Add(stateElement);
        }

        unsafe Type GetValueType()
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(identifier);

            var typeIndex = header->typeIndex;

            return DataType.Types[typeIndex].type;
        }

        internal unsafe MemoryIdentifier GetPortValue(GraphNodePort graphNodePort)
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(identifier);

            var payload = (byte*)(header + 1);

            var typeIndex = header->typeIndex;

            var dataType = DataType.Types[typeIndex];

            foreach (var field in dataType.inputFields)
            {
                if (field.info == graphNodePort.field)
                {
                    return
                        *(MemoryIdentifier*)(
                            payload + field.offset);
                }
            }

            foreach (var field in dataType.outputFields)
            {
                if (field.info == graphNodePort.field)
                {
                    return
                        *(MemoryIdentifier*)(
                            payload + field.offset);
                }
            }

            return MemoryIdentifier.Invalid;
        }

        public virtual unsafe void DrawDefaultInspector()
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var header = memoryChunk.GetHeader(identifier);

            var typeIndex = header->typeIndex;

            var dataType = DataType.Types[typeIndex];

            foreach (var field in dataType.propertyFields)
            {
                var fieldInfo = field.info;

                name = field.name;

                var genericMethod =
                    fieldValueMethod.MakeGenericMethod(dataType.type);

                var fieldValue = genericMethod.Invoke(
                    this, new object[] { fieldInfo });

                //field.SetValue(nodeTarget, newValue);

                var element = FieldFactory.CreateField(
                    field.type, fieldValue, null, name);

                if (element != null)
                {
                    element.SetEnabled(false);

                    controlsContainer.Add(element);
                }
            }
        }

        unsafe object GetFieldValueGeneric<T>(FieldInfo fieldInfo) where T : struct
        {
            ref var memoryChunk = ref owner.memoryChunk.Ref;

            T value = UnsafeUtilityEx.AsRef<T>(
                memoryChunk.GetPayload(identifier));

            return fieldInfo.GetValue(value);
        }

        public virtual void OnCreate()
        {
        }

        public override bool IsMovable()
        {
            return false;
        }

        public virtual void OnSelected(ref MotionSynthesizer synthesizer)
        {
        }

        public virtual void OnPreLateUpdate(ref MotionSynthesizer synthesizer)
        {
        }
    }
}
