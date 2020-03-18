using System.Collections.Generic;
using Unity.Kinematica.Editor.GenericStruct;
using Unity.Kinematica.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    class MarkerEditor : VisualElement
    {
        GenericStructInspector m_PayloadInspector;
        MarkerAnnotation m_Marker;
        TaggedAnimationClip m_Clip;

        public MarkerEditor(MarkerAnnotation marker, TaggedAnimationClip clip)
        {
            m_Clip = clip;
            m_Marker = marker;
            UIElementsUtils.ApplyStyleSheet(BuilderWindow.k_Stylesheet, this);
            UIElementsUtils.CloneTemplateInto("Inspectors/MarkerEditor.uxml", this);
            AddToClassList("drawerElement");

            if (!marker.payload.ValidPayloadType)
            {
                Clear();
                var unknownLabel = new Label { text = MarkerAttribute.k_UnknownMarkerType };
                unknownLabel.AddToClassList(AnnotationsEditor.k_UnknownAnnotationType);
                Add(unknownLabel);
                return;
            }

            TextField typeLabel = this.Q<TextField>();
            typeLabel.value = MarkerAttribute.GetDescription(m_Marker.payload.Type);
            typeLabel.SetEnabled(false);

            FloatField timeField = this.Q<FloatField>();
            timeField.value = marker.timeInSeconds;

            timeField.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                if (!EqualityComparer<float>.Default.Equals(m_Marker.timeInSeconds, evt.newValue))
                {
                    Undo.RecordObject(m_Clip.Asset, "Change marker time");
                    m_Marker.timeInSeconds = evt.newValue;
                    m_Marker.NotifyChanged();
                    clip.Asset.MarkDirty();
                }
            });

            m_Marker.Changed += UpdateTime;

            m_PayloadInspector = UnityEditor.Editor.CreateEditor(m_Marker.payload.ScriptableObject) as GenericStructInspector;

            m_PayloadInspector.StructModified += () =>
            {
                m_Marker.payload.Serialize();
                m_Clip.Asset.MarkDirty();
            };

            VisualElement inspectorElement = m_PayloadInspector.CreateInspectorGUI() ?? new IMGUIContainer(m_PayloadInspector.OnInspectorGUI);
            var inspectorContainer = this.Q<VisualElement>("payloadInspector");
            inspectorContainer.Add(inspectorElement);

            var deleteButton = this.Q<Button>("deleteButton");
            deleteButton.clickable.clicked += () => { RemoveMarker(m_Marker); };


            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            m_Clip.Asset.AssetWasDeserialized += UpdateTime;
        }

        void UpdateTime(Asset unused)
        {
            UpdateTime();
        }

        void UpdateTime()
        {
            FloatField timeField = this.Q<FloatField>();

            if (m_Marker == null)
            {
                return;
            }

            if (!EqualityComparer<float>.Default.Equals(timeField.value, m_Marker.timeInSeconds))
            {
                timeField.SetValueWithoutNotify(m_Marker.timeInSeconds);
            }
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (m_Marker != null)
            {
                m_Marker.Changed -= UpdateTime;
            }

            if (m_PayloadInspector != null)
            {
                Object.DestroyImmediate(m_PayloadInspector);
                m_PayloadInspector = null;
                ManipulatorGizmo.Instance.Unhook();
            }

            m_Clip.Asset.AssetWasDeserialized -= UpdateTime;
        }

        void RemoveMarker(MarkerAnnotation marker)
        {
            m_Clip.RemoveMarker(marker);
        }
    }
}
