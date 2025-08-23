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

    public static unsafe void ExtractFrustumPlanes(this Camera camera, Plane* planes)
    {
        Geometry.ExtractPlanes(camera.projectionMatrix * camera.worldToCameraMatrix, planes);
    }

    public static unsafe void ExtractFrustumPlanes(this Camera camera, NativeArray<Plane> planes)
    {
        camera.ExtractFrustumPlanes(planes.GetPtr());
    }
}
