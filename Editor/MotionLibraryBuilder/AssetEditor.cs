using System.Linq;
using Unity.Kinematica.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Kinematica.Editor
{
    [CustomEditor(typeof(Asset))]
    internal class AssetEditor : UnityEditor.Editor
    {
        const string k_TemplatePath = "Inspectors/AssetEditor.uxml";
        const string k_Stylesheet = "Inspectors/AssetEditor.uss";

        const float k_MinTimeHorizon = 1f;
        const float k_MaxTimeHorizon = 5f;

        const float k_MinSampleRate = 1f;
        const float k_MaxSampleRate = 120f;

        VisualElement m_Root;

        MetricsEditor m_MetricsEditor;
        VisualElement m_AssetSettingsInput;

        FloatField m_TimeHorizonInput;
        Slider m_TimeHorizonSlider;

        public override VisualElement CreateInspectorGUI()
        {
            m_Root = new VisualElement();
            UIElementsUtils.ApplyStyleSheet(BuilderWindow.k_Stylesheet, m_Root);
            UIElementsUtils.CloneTemplateInto(k_TemplatePath, m_Root);
            m_Root.AddToClassList("mainContainer");

            Asset asset = target as Asset;

            m_AssetSettingsInput = m_Root.Q<VisualElement>("assetSettings");
            //set restrictions on asset settings
            var sampleRateInput = m_AssetSettingsInput.Q<FloatField>("sampleRate");
            var sampleRateSlider = m_AssetSettingsInput.Q<Slider>("sampleRateSlider");
            float sampleRate = Mathf.Clamp(asset.SampleRate, 1f, asset.SampleRate);

            sampleRateInput.value = sampleRate;
            sampleRateSlider.value = sampleRate;
            sampleRateInput.RegisterValueChangedCallback(evt =>
            {
                float newSampleRate = Mathf.Clamp(evt.newValue, k_MinSampleRate, k_MaxSampleRate);
                asset.SampleRate = newSampleRate;
                sampleRateSlider.SetValueWithoutNotify(newSampleRate);
                ClampTimeHorizonInput(1f / newSampleRate);
            });
            sampleRateSlider.RegisterValueChangedCallback(evt =>
            {
                float newSampleRate = Mathf.Clamp(evt.newValue, k_MinSampleRate, k_MaxSampleRate);
                asset.SampleRate = newSampleRate;
                sampleRateInput.SetValueWithoutNotify(newSampleRate);
                ClampTimeHorizonInput(1f / newSampleRate);
            });
            sampleRateInput.SetFloatFieldRange(sampleRateSlider.lowValue, sampleRateSlider.highValue);

            m_TimeHorizonInput = m_AssetSettingsInput.Q<FloatField>("timeHorizon");
            m_TimeHorizonSlider = m_AssetSettingsInput.Q<Slider>("timeHorizonSlider");
            m_TimeHorizonSlider.value = asset.TimeHorizon;
            m_TimeHorizonInput.value = asset.TimeHorizon;
            m_TimeHorizonInput.RegisterValueChangedCallback(evt =>
            {
                float newTimeHorizon = Mathf.Clamp(evt.newValue, k_MinTimeHorizon, k_MaxTimeHorizon);
                asset.TimeHorizon = newTimeHorizon;
                m_TimeHorizonSlider.SetValueWithoutNotify(newTimeHorizon);
            });
            m_TimeHorizonSlider.RegisterValueChangedCallback(evt =>
            {
                float newTimeHorizon = Mathf.Clamp(evt.newValue, k_MinTimeHorizon, k_MaxTimeHorizon);
                asset.TimeHorizon = newTimeHorizon;
                m_TimeHorizonInput.SetValueWithoutNotify(newTimeHorizon);
            });

            m_TimeHorizonSlider.lowValue = 1f / sampleRateSlider.lowValue;
            m_TimeHorizonInput.SetFloatFieldRange(m_TimeHorizonSlider.lowValue, m_TimeHorizonSlider.highValue);

            var avatarSelector = m_Root.Q<ObjectField>("destinationAvatar");
            avatarSelector.objectType = typeof(Avatar);

            avatarSelector.value = asset.DestinationAvatar;
            avatarSelector.RegisterValueChangedCallback(OnAvatarSelectionChanged);

            UIElementsUtils.ApplyStyleSheet(k_Stylesheet, m_Root);

            m_MetricsEditor = new MetricsEditor(asset);
            m_Root.Q<VisualElement>("metrics").Add(m_MetricsEditor);

            var buildButton = m_Root.Q<Button>("buildButton");
            buildButton.clickable.clicked += BuildButtonClicked;

            if (EditorApplication.isPlaying)
            {
                SetInputEnabled(false);
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            return m_Root;
        }

        void OnDisable()
        {
            if (m_Root != null)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            SetInputEnabled(state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode);
        }

        void SetInputEnabled(bool enable)
        {
            m_AssetSettingsInput.SetEnabled(enable);
            m_MetricsEditor.SetInputEnabled(enable);
        }

        void ClampTimeHorizonInput(float lowValue)
        {
            m_TimeHorizonInput.SetFloatFieldRange(lowValue);
            m_TimeHorizonSlider.lowValue = lowValue;
        }

        void OnAvatarSelectionChanged(ChangeEvent<Object> evt)
        {
            var asset = target as Asset;
            if (asset != null)
            {
                asset.DestinationAvatar = evt.newValue as Avatar;
                m_MetricsEditor.OnAvatarChanged();
            }
        }

        void BuildButtonClicked()
        {
            if (target != null)
            {
                Selection.activeObject = target;
            }

            BuilderWindow.ShowWindow();
        }
    }
}
