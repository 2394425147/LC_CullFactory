using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace CullFactoryBurst
{

    [BurstCompile]
    public static class Geometry
    {
        [BurstDiscard]
        public static void WarnNotBursted()
        {
            Debug.LogWarning("Call to TestPlanesAABB is not using Burst.");
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        public static unsafe bool TestPlanesAABB(Plane* planes, long planeCount, in Bounds bounds)
        {
            WarnNotBursted();

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
                    return TestPlanesAABB(planePtr, planes.LongLength, bounds);
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