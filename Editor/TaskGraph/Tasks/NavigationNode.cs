namespace Unity.Kinematica.Editor
{
    [GraphNode(typeof(NavigationTask))]
    internal class NavigationNode : GraphNode
    {
        public override void OnSelected(ref MotionSynthesizer synthesizer)
        {
            ref var binary = ref synthesizer.Binary;

            ref var memoryChunk = ref owner.memoryChunk.Ref;

            var navTask = memoryChunk.GetRef<NavigationTask>(
                identifier).Ref;

            if (navTask.IsPathValid)
            {
                navTask.DrawPath();
            }
        }
    }
}
