//using UnityEngine.Assertions;

//namespace Unity.Kinematica
//{
//    [Data("Select", "#562A37"), BurstCompile]
//    public struct SelectTask : Task
//    {
//        [Input(typeof(WeightedSamplingTime), "Input")]
//        public MemoryIdentifier input;

//        [Input(typeof(Constant), "Constant")]
//        public MemoryIdentifier constant;

//        [Output(typeof(TimeIndex), "Time Index")]
//        public MemoryIdentifier timeIndex;

//        public Result Execute(ref MotionSynthesizer synthesizer)
//        {
//            return Result.Success;
//        }

//        public void DebugDraw(ref MotionSynthesizer synthesizer)
//        {

//        }

//        public static SelectTask Create(MemoryIdentifier input, MemoryIdentifier constant, MemoryIdentifier timeIndex)
//        {
//            return new SelectTask
//            {
//                input = input,
//                constant = constant,
//                timeIndex = timeIndex
//            };
//        }
//    }
//}
