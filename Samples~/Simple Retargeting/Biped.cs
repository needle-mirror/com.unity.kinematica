using UnityEngine;

using Unity.Kinematica;
using Unity.Mathematics;

namespace SimpleRetargeting
{
    [RequireComponent(typeof(Kinematica))]
    public class Biped : MonoBehaviour
    {
        [Header("Prediction settings")]
        [Tooltip("Desired speed in meters per second for slow movement.")]
        [Range(0.0f, 10.0f)]
        public float desiredSpeedSlow = 3.9f;

        [Tooltip("Desired speed in meters per second for fast movement.")]
        [Range(0.0f, 10.0f)]
        public float desiredSpeedFast = 5.5f;

        [Tooltip("How fast or slow the target velocity is supposed to be reached.")]
        [Range(0.0f, 1.0f)]
        public float velocityPercentage = 1.0f;

        [Tooltip("How fast or slow the desired forward direction is supposed to be reached.")]
        [Range(0.0f, 1.0f)]
        public float forwardPercentage = 1.0f;

        [Tooltip("Relative weighting for pose and trajectory matching.")]
        [Range(0.0f, 1.0f)]
        public float responsiveness = 0.5f;

        Identifier<ActionTask> locomotion;

        float3 movementDirection = Missing.forward;

        float desiredLinearSpeed => Input.GetButton("A Button") ? desiredSpeedFast : desiredSpeedSlow;

        void OnEnable()
        {
            var kinematica = GetComponent<Kinematica>();

            ref var synthesizer = ref kinematica.Synthesizer.Ref;

            synthesizer.Push(
                synthesizer.Query.Where(
                    Locomotion.Default).And(Idle.Default));

            var action = synthesizer.Action();

            action.PushConstrained(
                synthesizer.Query.Where(
                    Locomotion.Default).Except(Idle.Default),
                action.TrajectoryPrediction().trajectory);

            locomotion = action;
        }

        void Update()
        {
            var kinematica = GetComponent<Kinematica>();

            ref var synthesizer = ref kinematica.Synthesizer.Ref;

            synthesizer.Tick(locomotion);

            ref var prediction = ref synthesizer.GetByType<TrajectoryPredictionTask>(locomotion).Ref;

            ref var reduce = ref synthesizer.GetByType<ReduceTask>(locomotion).Ref;

            reduce.responsiveness = responsiveness;

            var horizontal = InputUtility.GetMoveHorizontalInput();
            ;
            var vertical = InputUtility.GetMoveVerticalInput();

            float3 analogInput = Utility.GetAnalogInput(horizontal, vertical);

            prediction.velocityFactor = velocityPercentage;
            prediction.rotationFactor = forwardPercentage;

            if (math.length(analogInput) >= 0.1f)
            {
                movementDirection =
                    Utility.GetDesiredForwardDirection(
                        analogInput, movementDirection);

                prediction.linearSpeed =
                    math.length(analogInput) *
                    desiredLinearSpeed;

                prediction.movementDirection = movementDirection;
                prediction.forwardDirection = movementDirection;
            }
            else
            {
                prediction.linearSpeed = 0.0f;
            }
        }

        void OnGUI()
        {
            InputUtility.DisplayMissingInputs(InputUtility.MoveInput);
        }
    }
}
