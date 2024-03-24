using CullFactory.Services;
using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Data;

public sealed class GrabbableObjectContents
{
    public readonly GrabbableObject item;
    public Renderer[] renderers;
    public Light[] lights;
    public Vector3[] boundingVertices;
    public Bounds bounds;

    public GrabbableObjectContents(GrabbableObject item)
    {
        this.item = item;
        CollectContents();
    }

    public void CollectContents()
    {
        renderers = item.GetComponentsInChildren<Renderer>();
        lights = item.GetComponentsInChildren<Light>();
        CalculateLocalBounds();
    }

    private void CalculateLocalBounds()
    {
        if (item == null)
        {
            boundingVertices = [Vector3.zero];
            return;
        }

        var min = Vector3.positiveInfinity;
        var max = Vector3.negativeInfinity;

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            if (!renderer.enabled)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
                continue;
            var rendererBounds = renderer.bounds;
            min = Vector3.Min(min, rendererBounds.min);
            max = Vector3.Max(max, rendererBounds.max);
        }

        boundingVertices = BoundsUtility.GetVertices(min, max);
        item.transform.InverseTransformPoints(boundingVertices);
        CalculateBounds();
    }

    public void CalculateBounds()
    {
        if (item == null)
        {
            bounds = default;
            return;
        }

        bounds = GeometryUtility.CalculateBounds(boundingVertices, item.transform.localToWorldMatrix);
    }

    public bool IsVisible(Plane[] planes)
    {
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    public bool IsVisible(Plane[][] planes, int lastIndex)
    {
        for (var i = 0; i <= lastIndex; i++)
        {
            if (!IsVisible(planes[i]))
                return false;
        }
        return true;
    }

    public bool IsWithin(Bounds bounds)
    {
        return this.bounds.Intersects(bounds);
    }

    public bool IsWithin(TileContents tile)
    {
        return IsWithin(tile.bounds);
    }

    public bool IsWithin(IEnumerable<TileContents> tiles)
    {
        foreach (var tile in tiles)
        {
            if (IsWithin(tile))
                return true;
        }
        return false;
    }

    public void SetVisible(bool visible)
    {
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            renderer.forceRenderingOff = !visible;
        }
    }

    public override string ToString()
    {
        if (item == null)
            return "Destroyed";
        return item.name;
    }

    public override int GetHashCode()
    {
        return item.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (obj is GrabbableObjectContents contents)
            return item.Equals(contents.item);
        return false;
    }
}
