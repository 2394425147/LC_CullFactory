using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Data;

public sealed class GrabbableObjectContents
{
    public readonly GrabbableObject item;
    public Renderer[] renderers;
    public Light[] lights;
    public Bounds localBounds;

    private bool _wasVisible = true;

    public GrabbableObjectContents(GrabbableObject item)
    {
        this.item = item;
        CollectContents();
    }

    public void CollectContents()
    {
        renderers = item.GetComponentsInChildren<Renderer>();
        lights = item.GetComponentsInChildren<Light>();
    }

    public bool IsVisible(Plane[] planes)
    {
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            if (!renderer.enabled)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
                continue;
            if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                return true;
        }
        return false;
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
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            if (!renderer.enabled)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer.bounds.Intersects(bounds))
                return true;
        }
        return false;
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
        if (_wasVisible == visible)
            return;

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            renderer.forceRenderingOff = !visible;
        }

        _wasVisible = visible;
    }

    public override string ToString()
    {
        if (item == null)
            return "Destroyed";
        return item.name;
    }
}
