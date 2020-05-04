using Unity.Kinematica;
using Unity.Mathematics;
using UnityEngine;

namespace SnappyLocomotion
{
    [RequireComponent(typeof(Kinematica))]
    public class SnappyLocomotion : MonoBehaviour
    {
        [Tooltip("How much character translation should match desired trajectory as opposed to the binary.")]
        [Range(0.0f, 1.0f)]
        public float snapTranslationFactor = 1.0f;

        [Tooltip("How much character rotation should match desired trajectory as opposed to the binary.")]
        [Range(0.0f, 1.0f)]
        public float snapRotationFactor = 1.0f;


        Identifier<SelectorTask> locomotion;
        Identifier<Trajectory> desiredTrajectory;

        float3 movementDirection = Vector3.forward;

        Kinematica kinematica;
        CharacterController controller;

        void OnEnable()
        {
            kinematica = GetComponent<Kinematica>();
            controller = GetComponent<CharacterController>();

            ref var synthesizer = ref kinematica.Synthesizer.Ref;

            synthesizer.Push(
                synthesizer.Query.Where(
                    Locomotion.Default).And(Idle.Default));

            ref var selector = ref synthesizer.Selector();

            {
                ref var sequence = ref selector.Condition().Sequence();

                sequence.Action().PushConstrained(
                    synthesizer.Query.Where(
                        Locomotion.Default).And(Idle.Default), 0.01f);

                sequence.Action().Timer();
            }

            {
                ref var action = ref selector.Action();

                ref var prediction = ref action.TrajectoryPrediction();
                prediction.velocityFactor = 0.2f;
                prediction.rotationFactor = 0.1f;
                desiredTrajectory = prediction.trajectory;

                action.PushConstrained(
                    synthesizer.Query.Where(
                        Locomotion.Default).Except(Idle.Default),
                            prediction.trajectory);
            }

            locomotion = selector;
        }

        public virtual void OnAnimatorMove()
        {
            if (kinematica.Synthesizer.IsValid)
            {
                ref MotionSynthesizer synthesizer = ref kinematica.Synthesizer.Ref;

                AffineTransform rootDelta = synthesizer.SteerRootDeltaTransform(desiredTrajectory, snapTranslationFactor, snapRotationFactor);

                transform.rotation *= rootDelta.q;
    #if UNITY_EDITOR
                if (Unity.SnapshotDebugger.Debugger.instance.rewind)
                {
                    return;
                }
    #endif
                controller.Move(transform.TransformVector(rootDelta.t));
                synthesizer.SetWorldTransform(AffineTransform.Create(transform.position, transform.rotation), true);
            }
        }

        void Update()
        {
            ref var synthesizer = ref kinematica.Synthesizer.Ref;
            synthesizer.Tick(locomotion);

            ref var prediction = ref synthesizer.GetByType<TrajectoryPredictionTask>(locomotion).Ref;
            ref var idle = ref synthesizer.GetByType<ConditionTask>(locomotion).Ref;

            var horizontal = InputUtility.GetMoveHorizontalInput();
            var vertical = InputUtility.GetMoveVerticalInput();

            float3 analogInput = Utility.GetAnalogInput(horizontal, vertical);

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
                        3.9f;

                prediction.movementDirection = movementDirection;
                prediction.forwardDirection = movementDirection;
            }
        }

        void OnGUI()
        {
            InputUtility.DisplayMissingInputs(InputUtility.MoveInput);
        }
    }
}