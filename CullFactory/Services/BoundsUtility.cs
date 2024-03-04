using UnityEngine;

namespace CullFactory.Services;

public static class BoundsUtility
{
    public static Plane[] GetPlanes(this Bounds bounds)
    {
        return [
            new Plane(new Vector3(1, 0, 0), -bounds.min.x),
            new Plane(new Vector3(0, 1, 0), -bounds.min.y),
            new Plane(new Vector3(0, 0, 1), -bounds.min.z),
            new Plane(new Vector3(-1, 0, 0), bounds.max.x),
            new Plane(new Vector3(0, -1, 0), bounds.max.y),
            new Plane(new Vector3(0, 0, -1), bounds.max.z),
        ];
    }

    public static void GetFarthestPlanesNonAlloc(this Bounds bounds, Vector3 point, Plane[] planes)
    {
        planes[0] = point.x > bounds.center.x
                ? new Plane(new Vector3(1, 0, 0), -bounds.min.x)
                : new Plane(new Vector3(-1, 0, 0), bounds.max.x);
        planes[1] = point.y > bounds.center.y
                ? new Plane(new Vector3(0, 1, 0), -bounds.min.y)
                : new Plane(new Vector3(0, -1, 0), bounds.max.y);
        planes[2] = point.z > bounds.center.z
                ? new Plane(new Vector3(0, 0, 1), -bounds.min.z)
                : new Plane(new Vector3(0, 0, -1), bounds.max.z);
    }

    public static Plane[] GetFarthestPlanes(this Bounds bounds, Vector3 point)
    {
        var planes = new Plane[3];
        bounds.GetFarthestPlanesNonAlloc(point, planes);
        return planes;
    }
}
