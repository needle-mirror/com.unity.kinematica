using UnityEngine;

using Unity.Kinematica;
using Unity.Mathematics;

namespace BipedLocomotion
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

        Identifier<SelectorTask> locomotion;

        float3 movementDirection = Missing.forward;

        float desiredLinearSpeed => InputUtility.IsPressingActionButton() ? desiredSpeedFast : desiredSpeedSlow;

        void OnEnable()
        {
            var kinematica = GetComponent<Kinematica>();

            ref var synthesizer = ref kinematica.Synthesizer.Ref;

            synthesizer.Push(
                synthesizer.Query.Where(
                    Locomotion.Default).And(Idle.Default));

            var selector = synthesizer.Selector();

            {
                var sequence = selector.Condition().Sequence();

                sequence.Action().PushConstrained(
                    synthesizer.Query.Where(
                        Locomotion.Default).And(Idle.Default), 0.01f);

                sequence.Action().Timer();
            }

            {
                var action = selector.Action();

                action.PushConstrained(
                    synthesizer.Query.Where(
                        Locomotion.Default).Except(Idle.Default),
                    action.TrajectoryPrediction().trajectory);
            }

            locomotion = selector;
        }

        void Update()
        {
            var kinematica = GetComponent<Kinematica>();

            ref var synthesizer = ref kinematica.Synthesizer.Ref;

            synthesizer.Tick(locomotion);

            ref var prediction = ref synthesizer.GetByType<TrajectoryPredictionTask>(locomotion).Ref;
            ref var idle = ref synthesizer.GetByType<ConditionTask>(locomotion).Ref;

            var horizontal = InputUtility.GetMoveHorizontalInput();
            var vertical = InputUtility.GetMoveVerticalInput();

            float3 analogInput = Utility.GetAnalogInput(horizontal, vertical);

            prediction.velocityFactor = velocityPercentage;
            prediction.rotationFactor = forwardPercentage;

            idle.value = math.length(analogInput) <= 0.1f;

            if (idle)
            {
                prediction.linearSpeed = 0.0f;
            }
            else
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
        }

        void OnGUI()
        {
            InputUtility.DisplayMissingInputs(InputUtility.ActionButtonInput | InputUtility.MoveInput | InputUtility.CameraInput);
        }
    }
}
