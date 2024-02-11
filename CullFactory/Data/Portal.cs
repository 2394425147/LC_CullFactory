using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public class Portal
{
    public Vector3[] corners { get; private set; }
    public Bounds bounds { get; private set; }

    public Portal(Doorway doorway)
    {
        var doorwayTransform = doorway.transform;
        var size = doorway.Socket.Size;
        var halfWidth = size.x / 2;

        corners =
        [
            new Vector3(halfWidth, 0, 0),
            new Vector3(halfWidth, size.y, 0),
            new Vector3(-halfWidth, size.y, 0),
            new Vector3(-halfWidth, 0, 0),
        ];
        for (var i = 0; i < corners.Length; i++)
            corners[i] = doorwayTransform.position + doorwayTransform.rotation * corners[i];

        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;
        foreach (var corner in corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        bounds = new Bounds
        {
            min = min,
            max = max
        };
    }

    internal void GetFrustumPlanes(Vector3 origin, Plane[] planes)
    {
        planes[0] = new Plane(corners[0], corners[1], origin);
        planes[1] = new Plane(corners[1], corners[2], origin);
        planes[2] = new Plane(corners[2], corners[3], origin);
        planes[3] = new Plane(corners[3], corners[0], origin);
        planes[4] = new Plane(corners[0], corners[1], corners[3]);
    }

    internal Plane[] GetFrustumPlanes(Vector3 origin)
    {
        var planes = new Plane[5];
        GetFrustumPlanes(origin, planes);
        return planes;
    }
}
