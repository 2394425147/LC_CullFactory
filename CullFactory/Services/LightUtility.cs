using UnityEngine;

namespace CullFactory.Services
{
    public static class LightUtility
    {
        public static bool Affects(this Light light, Bounds bounds)
        {
            var lightRange = light.range;
            var lightDistanceSquared = bounds.SqrDistance(light.transform.position);
            if (lightDistanceSquared > lightRange * lightRange)
                return false;
            // TODO: Take into account light shapes other than point.
            return true;
        }
    }
}
