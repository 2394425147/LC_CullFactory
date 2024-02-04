using CullFactory.Data;
using DunGen;
using UnityEngine;

namespace CullFactory.Utilities;

public static class FrustumUtilities
{
    public static bool TryTrimFrustum(this Doorway door, Camera camera, Plane[] frustum, out FrustumAtDoor result)
    {
        result = new FrustumAtDoor(door, frustum);
        return false;
    }
}
