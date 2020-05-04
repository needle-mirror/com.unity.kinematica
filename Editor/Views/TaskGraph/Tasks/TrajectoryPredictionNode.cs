using UnityEngine;
using UnityEngine.UIElements;

using Unity.Mathematics;

namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(TrajectoryPredictionTask))]
    internal class TrajectoryPredictionNode : GraphNode
    {
        Label label = new Label();

        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            ref var binary = ref synthesizer.Binary;

            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var prediction =
                memoryChunk.GetRef<TrajectoryPredictionTask>(
                    identifier).Ref;

            var worldRootTransform = synthesizer.WorldRootTransform;

            DisplayAxis(worldRootTransform.t, prediction.movementDirection, Color.white);

            label.text = $"Linear speed: {prediction.linearSpeed}";
        }

        void DisplayAxis(float3 startPosition, float3 axis, Color color)
        {
            float height = 0.1f;
            float width = height * 0.5f;

            var endPosition = startPosition + axis;

            var length = math.length(axis);

            if (length >= height)
            {
                var normalizedDirection = axis * math.rcp(length);

                var rotation =
                    Missing.forRotation(Missing.up, normalizedDirection);

                DebugDraw.DrawLine(startPosition, endPosition, color);

                DebugDraw.DrawCone(
                    endPosition - normalizedDirection * height,
                    rotation, width, height, color);
            }
        }

        public override void DrawDefaultInspector()
        {
            controlsContainer.Add(label);
        }
    }
}
