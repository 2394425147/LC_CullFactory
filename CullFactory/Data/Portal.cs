using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public class Portal
{
    public Vector3[] Corners { get; private set; }
    public Bounds Bounds { get; private set; }

    public Portal(Doorway doorway, bool useTileBounds = false)
    {
        var doorwayTransform = doorway.transform;
        var horizontalExtent = doorway.Socket.Size.x / 2;
        var height = doorway.Socket.Size.y;

        if (useTileBounds)
        {
            var localTileBounds = doorwayTransform.InverseTransformBounds(doorway.Tile.Bounds);
            horizontalExtent = Mathf.Min(-localTileBounds.min.x, localTileBounds.max.x);
            height = localTileBounds.max.y;
        }

        Corners =
        [
            new Vector3(horizontalExtent, 0, 0),
            new Vector3(horizontalExtent, height, 0),
            new Vector3(-horizontalExtent, height, 0),
            new Vector3(-horizontalExtent, 0, 0),
        ];

        for (var i = 0; i < Corners.Length; i++)
            Corners[i] = doorwayTransform.position + doorwayTransform.rotation * Corners[i];

        var min = Corners[0];
        var max = Corners[0];

        for (var index = 1; index < Corners.Length; index++)
        {
            var corner = Corners[index];

            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        Bounds = new Bounds
        {
            min = min,
            max = max
        };
    }

    internal void GetFrustumPlanesNonAlloc(Vector3 origin, Plane[] planes)
    {
        planes[0] = new Plane(Corners[0], Corners[1], origin);
        planes[1] = new Plane(Corners[1], Corners[2], origin);
        planes[2] = new Plane(Corners[2], Corners[3], origin);
        planes[3] = new Plane(Corners[3], Corners[0], origin);
    }

    internal Plane[] GetFrustumPlanes(Vector3 origin)
    {
        var planes = new Plane[4];
        GetFrustumPlanesNonAlloc(origin, planes);
        return planes;
    }
}
