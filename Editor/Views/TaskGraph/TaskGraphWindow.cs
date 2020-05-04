using System;
using Unity.Kinematica.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEngine.Assertions;

using Unity.SnapshotDebugger;

using PreLateUpdate = UnityEngine.PlayerLoop.PreLateUpdate;
using UnityEngine.Rendering;

namespace Unity.Kinematica.Editor
{
    internal class TaskGraphWindow : EditorWindow
    {
        [NonSerialized]
        Kinematica kinematica;

        readonly string styleSheet = "NodeGraphWindow.uss";
        readonly string toolbarStyleSheet = "NodeGraphToolbar.uss";

        [SerializeField]
        bool m_ExitingPlayMode;

        private TaskGraphWindow()
        {
        }

        [MenuItem("Window/Animation/Kinematica Task Graph")]
        public static void ShowWindow()
        {
            GetWindow<TaskGraphWindow>("Kinematica Task Graph");
        }

        void OnEnable()
        {
            LoadTemplate();

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            //For regular RP
            Camera.onPostRender -= OnRender;
            Camera.onPostRender += OnRender;

            //For HDRP
            RenderPipelineManager.endCameraRendering -= OnRenderRP;
            RenderPipelineManager.endCameraRendering += OnRenderRP;

            UpdateSystem.Listen<PreLateUpdate>(OnPreLateUpdate);

            m_ExitingPlayMode = false;
        }

        void OnDisable()
        {
            UpdateSystem.Ignore<PreLateUpdate>(OnPreLateUpdate);

            Camera.onPostRender -= OnRender;
            RenderPipelineManager.endCameraRendering -= OnRenderRP;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        void LoadTemplate()
        {
            UIElementsUtils.ApplyStyleSheet(styleSheet, rootVisualElement);
            UIElementsUtils.ApplyStyleSheet(toolbarStyleSheet, rootVisualElement);

            UIElementsUtils.CloneTemplateInto("NodeGraphWindow.uxml", rootVisualElement);

            var breadcrumb = rootVisualElement.Q<Breadcrumb>("breadcrumb");
            breadcrumb.Clear();
            breadcrumb.PushItem("Root");

            var content = rootVisualElement.Q<VisualElement>("content");
            var graphNodeView = new GraphNodeView(this);
            graphNodeView.name = "graphNodeView";
            content.Add(graphNodeView);

            DisplayMessages();
        }

        bool DisplayMessages()
        {
            var playModeMessage = rootVisualElement.Q("playModeMessage");
            var selectionMessage = rootVisualElement.Q("selectionMessage");
            var graphNodeView = rootVisualElement.Q("graphNodeView");

            if (!EditorApplication.isPlaying)
            {
                playModeMessage.style.display = DisplayStyle.Flex;
                selectionMessage.style.display = DisplayStyle.None;
                graphNodeView.style.display = DisplayStyle.None;
                return true;
            }

            if (kinematica == null)
            {
                playModeMessage.style.display = DisplayStyle.None;
                selectionMessage.style.display = DisplayStyle.Flex;
                graphNodeView.style.display = DisplayStyle.None;
                return true;
            }

            playModeMessage.style.display = DisplayStyle.None;
            selectionMessage.style.display = DisplayStyle.None;
            graphNodeView.style.display = DisplayStyle.Flex;

            return true;
        }

        void OnPreLateUpdate()
        {
            if (EditorApplication.isPlaying && (kinematica != null))
            {
                var graphNodeView =
                    rootVisualElement.Q<GraphNodeView>("graphNodeView");

                var synthesizer = kinematica.Synthesizer;

                foreach (var selectable in graphNodeView.selection)
                {
                    var graphNode = selectable as GraphNode;

                    if (graphNode != null)
                    {
                        graphNode.OnPreLateUpdate(ref synthesizer.Ref);
                    }
                }
            }
        }

        void OnRenderRP(ScriptableRenderContext context, Camera camera)
        {
            OnRender(camera);
        }

        void OnRender(Camera camera)
        {
            if (EditorApplication.isPlaying && kinematica != null && !m_ExitingPlayMode)
            {
                var graphNodeView =
                    rootVisualElement.Q<GraphNodeView>("graphNodeView");

                var synthesizer = kinematica.Synthesizer;

                DebugDraw.Begin(camera);

                DebugDraw.SetDepthRendering(false);

                foreach (var selectable in graphNodeView.selection)
                {
                    var graphNode = selectable as GraphNode;

                    if (graphNode != null)
                    {
                        graphNode.OnSelected(ref synthesizer.Ref);
                    }
                }

                DebugDraw.End();
            }
        }

        void OnSelectionChange()
        {
            var activeGameObject = Selection.activeGameObject;

            if (!activeGameObject)
            {
                activeGameObject = FindKinematicaObject();
            }

            Kinematica component = null;

            if (activeGameObject)
            {
                component =
                    activeGameObject.GetComponent<Kinematica>();
            }

            if (component != kinematica)
            {
                kinematica = component;
            }
            else
            {
                kinematica = null;
            }

            DisplayMessages();
        }

        void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                m_ExitingPlayMode = true;
            }

            OnSelectionChange();
        }

        GameObject FindKinematicaObject()
        {
            var kinematica = FindObjectOfType(typeof(Kinematica)) as MonoBehaviour;

            if (kinematica != null)
            {
                return kinematica.gameObject;
            }

            return null;
        }

        void Update()
        {
            if (EditorApplication.isPlaying && kinematica == null)
            {
                OnSelectionChange();
            }

            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        void OnInspectorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        void OnGUI()
        {
            var graphNodeView =
                rootVisualElement.Q<GraphNodeView>("graphNodeView");

            if (EditorApplication.isPlaying && (kinematica != null))
            {
                var synthesizer = kinematica.Synthesizer;

                var memoryChunk =
                    synthesizer.Ref.memoryChunk;

                graphNodeView.Update(memoryChunk);
            }
            else
            {
                graphNodeView.Update(MemoryRef<MemoryChunk>.Null);
            }
        }
    }
}
