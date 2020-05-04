using Unity.Kinematica;
using UnityEngine;

namespace HelloWorld
{
    [RequireComponent(typeof(Kinematica))]
    public class HelloWorld : MonoBehaviour
    {
        bool idle;

        void Update()
        {
            if (Input.anyKeyDown)
            {
                idle ^= true;

                var kinematica = GetComponent<Kinematica>();

                ref var synthesizer = ref kinematica.Synthesizer.Ref;

                if (idle)
                {
                    synthesizer.Action().Push(
                        synthesizer.Query.Where(
                            Locomotion.Default).And(Idle.Default));
                }
                else
                {
                    synthesizer.Action().Push(
                        synthesizer.Query.Where(
                            Locomotion.Default).Except(Idle.Default));
                }
            }
        }
    }
}