using Unity.Burst;

namespace CullFactoryBurst
{

    [BurstCompile]
    public static class Plugin
    {
        [BurstDiscard]
        private static void SetValueFalseIfManaged(ref bool value)
        {
            value = false;
        }

        [BurstCompile]
        public static bool IsRunningBurstLibrary()
        {
            var result = true;
            SetValueFalseIfManaged(ref result);
            return result;
        }
    }

}