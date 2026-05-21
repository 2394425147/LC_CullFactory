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
            TempFrustum = new NativeArray<Plane>(5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        return camera.ExtractFrustumPlanes(TempFrustum);
    }

    public static unsafe NativeSlice<Plane> ExtractFrustumPlanes(this Camera camera, NativeArray<Plane> planes)
    {
        if (planes.Length < 5)
            throw new IndexOutOfRangeException("ExtractFrustumPlanes would write out of range");
        camera.ExtractFrustumPlanes(planes.GetPtr());
        return planes.Slice(0, 5);
    }

    public static unsafe NativeSlice<Plane> ExtractFrustumPlanes(this Camera camera, NativeSlice<Plane> planes)
    {
        if (planes.Length < 5)
            throw new IndexOutOfRangeException("ExtractFrustumPlanes would write out of range");
        camera.ExtractFrustumPlanes(planes.GetPtr());
        return planes.Slice(0, 5);
    }

    private static unsafe void ExtractFrustumPlanes(this Camera camera, Plane* planes)
    {
        Geometry.ExtractPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, planes);
    }
}
