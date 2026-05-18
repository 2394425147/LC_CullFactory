using System;
using CullFactoryBurst;
using Unity.Collections;
using UnityEngine;

namespace CullFactory.Services;

internal static class CameraUtility
{
    private static NativeArray<Plane> TempFrustum;

    public static unsafe NativeSlice<Plane> GetTempFrustum(this Camera camera)
    {
        if (TempFrustum.Length == 0)
            TempFrustum = new NativeArray<Plane>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        camera.ExtractFrustumPlanes(TempFrustum.GetPtr());
        return TempFrustum;
    }

    public static unsafe void ExtractFrustumPlanes(this Camera camera, NativeArray<Plane> planes)
    {
        if (planes.Length < 6)
            throw new IndexOutOfRangeException("ExtractFrustumPlanes would write out of range");
        camera.ExtractFrustumPlanes(planes.GetPtr());
    }

    private static unsafe void ExtractFrustumPlanes(this Camera camera, Plane* planes)
    {
        Geometry.ExtractPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, planes);

        // Replace the near plane with the camera origin so that we don't cut off doorway portals
        // closer to the camera than the actual near plane.
        var transform = camera.transform;
        planes[4] = new Plane(transform.forward, transform.position);
    }
}
