using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace CullFactoryBurst
{

    [BurstCompile]
    public static class Geometry
    {
        private static bool WarnedCallWasNotBurstCompiled = false;

        [BurstDiscard]
        public static void WarnIfNotBurstCompiled()
        {
            if (!WarnedCallWasNotBurstCompiled)
            {
                Debug.LogWarning("Call to TestPlanesAABB is not using Burst.");
                WarnedCallWasNotBurstCompiled = true;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        public static unsafe bool TestPlanesAABB(Plane* planes, int planeCount, in Bounds bounds)
        {
            WarnIfNotBurstCompiled();

            for (int i = 0; i < planeCount; i++)
            {
                var plane = planes[i];

                var centerDistance = (plane.normal.x * bounds.center.x) + (plane.normal.y * bounds.center.y) + (plane.normal.z * bounds.center.z);
                var extentsDistance = (math.abs(plane.normal.x) * bounds.extents.x) + (math.abs(plane.normal.y) * bounds.extents.y) + (math.abs(plane.normal.z) * bounds.extents.z);
                var result = centerDistance + extentsDistance + plane.distance;

                if (result <= 0)
                    return false;
            }

            return true;
        }

        public static bool TestPlanesAABB(in Plane[] planes, in Bounds bounds)
        {
            unsafe
            {
                fixed (Plane* planePtr = planes)
                {
                    return TestPlanesAABB(planePtr, planes.Length, bounds);
                }
            }
        }

        public static bool TestPlanesAABB(in Span<Plane> planes, in Bounds bounds)
        {
            unsafe
            {
                fixed (Plane* planePtr = planes)
                {
                    return TestPlanesAABB(planePtr, planes.Length, bounds);
                }
            }
        }
    }

}