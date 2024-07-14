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

            var min = new float3(bounds.center) - new float3(bounds.size);
            var max = new float3(bounds.center) + new float3(bounds.size);

            var cornersTransposed = stackalloc float4[6];
            cornersTransposed[0] = new(min.x, min.x, min.x, min.x);
            cornersTransposed[1] = new(min.y, min.y, max.y, max.y);
            cornersTransposed[2] = new(min.z, max.z, min.z, max.z);

            cornersTransposed[3] = new(max.x, max.x, max.x, max.x);
            cornersTransposed[4] = new(min.y, min.y, max.y, max.y);
            cornersTransposed[5] = new(min.z, max.z, min.z, max.z);

            for (int i = 0; i < planeCount; i++)
            {
                var plane = planes[i];

                var planeX = new float4(plane.normal.x);
                var planeY = new float4(plane.normal.y);
                var planeZ = new float4(plane.normal.z);
                var planeDistance = new float4(plane.distance);

                int counter = 2;

                for (var j = 0; j < 6; j += 3)
                {
                    var productX = planeX * cornersTransposed[j + 0];
                    var productY = planeY * cornersTransposed[j + 1];
                    var productZ = planeZ * cornersTransposed[j + 2];
                    var result = productX + productY + productZ + planeDistance;
                    if (math.all(result <= 0))
                        counter--;
                }

                if (counter == 0)
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