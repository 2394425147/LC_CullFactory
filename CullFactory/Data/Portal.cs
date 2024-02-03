using DunGen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using UnityEngine;

namespace CullFactory.Data;

public class Portal
{
    public Vector3[] Corners { get; private set; }
    public Bounds Bounds { get; private set; }

    public Portal(Doorway doorway)
    {
        var doorwayTransform = doorway.transform;
        var size = doorway.Socket.Size;
        var halfWidth = size.x / 2;

        Corners =
        [
            new Vector3(halfWidth, 0, 0),
            new Vector3(halfWidth, size.y, 0),
            new Vector3(-halfWidth, size.y, 0),
            new Vector3(-halfWidth, 0, 0),
        ];
        for (int i = 0; i < Corners.Length; i++)
            Corners[i] = doorwayTransform.position + doorwayTransform.rotation * Corners[i];

        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;
        foreach (var corner in Corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        Bounds = new Bounds
        {
            min = min,
            max = max
        };
    }

    internal void SetCorners(Vector3[] corners)
    {
        if (corners.Length != 4)
            throw new ArgumentException($"SetCorners() was called with {corners.Length} corners instead of 4");
        Corners = corners;

        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;
        foreach (var corner in corners)
        {
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        Bounds = new Bounds
        {
            min = min,
            max = max
        };
    }

    internal void GetFrustumPlanes(Vector3 origin, Plane[] planes)
    {
        planes[0] = new Plane(Corners[0], Corners[1], origin);
        planes[1] = new Plane(Corners[1], Corners[2], origin);
        planes[2] = new Plane(Corners[2], Corners[3], origin);
        planes[3] = new Plane(Corners[3], Corners[0], origin);
        planes[4] = new Plane(Corners[0], Corners[1], Corners[3]);
    }

    internal Plane[] GetFrustumPlanes(Vector3 origin)
    {
        var planes = new Plane[5];
        GetFrustumPlanes(origin, planes);
        return planes;
    }
}
