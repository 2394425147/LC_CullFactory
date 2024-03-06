using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Data;

public sealed class GrabbableObjectContents
{
    public readonly GrabbableObject item;
    public Renderer[] renderers;
    public Light[] lights;
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
        CalculateBounds();
    }

    public void CalculateBounds()
    {
        bounds = new Bounds(item.transform.position, Vector3.zero);

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            if (!renderer.enabled)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
                continue;
            bounds.Encapsulate(renderer.bounds);
        }
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
}
