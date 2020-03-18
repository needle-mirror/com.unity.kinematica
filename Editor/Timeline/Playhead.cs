using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ColorUtility = Unity.SnapshotDebugger.ColorUtility;

namespace Unity.Kinematica.Editor
{
    class Playhead : VisualElement
    {
        VisualElement m_Handle;
        bool m_DebugPlayhead;
        public Playhead(bool debugPlayhead)
        {
            m_DebugPlayhead = debugPlayhead;
            style.display = DisplayStyle.None;
            AddToClassList("playHead");

            m_Handle = new VisualElement() { name = "handle"};
            m_Handle.AddToClassList("playHeadHandle");

            m_ShowHandle = !EditorApplication.isPlaying;
            m_Handle.generateVisualContent += GenerateTickHeadContent;

            Add(m_Handle);
        }

        bool m_ShowHandle;

        public bool ShowHandle
        {
            set
            {
                if (m_ShowHandle != value)
                {
                    m_ShowHandle = value;
                    m_Handle.style.display = m_ShowHandle ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        public void GenerateTickHeadContent(MeshGenerationContext context)
        {
            MeshWriteData mesh = context.Allocate(3, 3);
            Vertex[] vertices = new Vertex[3];
            const float totalHeight = 20f;
            const float height = 6f;
            const float width = 11f;

            vertices[0].position = new Vector3(6, totalHeight, Vertex.nearZ);
            vertices[1].position = new Vector3(width, totalHeight - height, Vertex.nearZ);
            vertices[2].position = new Vector3(0, totalHeight - height, Vertex.nearZ);

            if (EditorGUIUtility.isProSkin)
            {
                var proColor = m_DebugPlayhead ? ColorUtility.FromHtmlString("#234A6C") : Color.white;
                vertices[0].tint = proColor;
                vertices[1].tint = proColor;
                vertices[2].tint = proColor;
            }
            else
            {
                //TODO - change to light theme

                var personalColor = m_DebugPlayhead ? ColorUtility.FromHtmlString("#2E5B8D") : Color.white;
                vertices[0].tint = personalColor;
                vertices[1].tint = personalColor;
                vertices[2].tint = personalColor;
            }

            mesh.SetAllVertices(vertices);
            mesh.SetAllIndices(new ushort[] { 0, 2, 1 });
        }
    }
}
