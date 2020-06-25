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

            synthesizer.PlayFirstSequence(
                synthesizer.Query.Where(
                    Locomotion.Default).And(Idle.Default));

            var selector = synthesizer.Root.Selector();

            {
                var sequence = selector.Condition().Sequence();

                sequence.Action().MatchPose(
                    synthesizer.Query.Where(
                        Locomotion.Default).And(Idle.Default), 0.01f);

                sequence.Action().Timer();
            }

            {
                var action = selector.Action();

                ref var prediction = ref action.TrajectoryPrediction().GetAs<TrajectoryPredictionTask>();
                prediction.velocityFactor = 0.2f;
                prediction.rotationFactor = 0.1f;
                desiredTrajectory = prediction.trajectory;

                action.MatchPoseAndTrajectory(
                    synthesizer.Query.Where(
                        Locomotion.Default).Except(Idle.Default),
                            prediction.trajectory);

                action.GetChildByType<MatchFragmentTask>().trajectoryWeight = 0.7f;
            }

            locomotion = selector.GetAs<SelectorTask>();
        }

        public virtual void OnAnimatorMove()
        {
            if (kinematica.Synthesizer.IsValid)
            {
                ref MotionSynthesizer synthesizer = ref kinematica.Synthesizer.Ref;

                ref var idle = ref synthesizer.GetChildByType<ConditionTask>(locomotion).Ref;

                AffineTransform rootDelta = synthesizer.SteerRootMotion(desiredTrajectory,
                    idle.value ? 0.0f : snapTranslationFactor,
                    idle.value ? 0.0f : snapRotationFactor);

                float3 rootTranslation = transform.rotation * rootDelta.t;
                transform.rotation *= rootDelta.q;
                
    #if UNITY_EDITOR
                if (Unity.SnapshotDebugger.Debugger.instance.rewind)
                {
                    return;
                }
    #endif
                controller.Move(rootTranslation);
                synthesizer.SetWorldTransform(AffineTransform.Create(transform.position, transform.rotation), true);
            }
        }

        void Update()
        {
            ref var synthesizer = ref kinematica.Synthesizer.Ref;
            synthesizer.Tick(locomotion);

            ref var prediction = ref synthesizer.GetChildByType<TrajectoryPredictionTask>(locomotion).Ref;
            ref var idle = ref synthesizer.GetChildByType<ConditionTask>(locomotion).Ref;

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