using CullFactory.Services;
using CullFactoryBurst;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace CullFactory.Data;

public sealed class GrabbableObjectContents(GrabbableObject item) : IEquatable<GrabbableObjectContents>
{
    public static readonly Vector3 Vector3NaN = new Vector3(float.NaN, float.NaN, float.NaN);

    public readonly GrabbableObject item = item;
    public Renderer[] renderers = [];
    public Light[] lights = [];
    public Vector3[] boundingVertices = [];
    public Bounds bounds;

    public EnemyAI heldByEnemy;

    public void CollectContents()
    {
        renderers = item.GetComponentsInChildren<Renderer>();
        lights = item.GetComponentsInChildren<Light>();
        CalculateLocalBounds();
    }

    private void CalculateLocalBounds()
    {
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

        if (min.Equals(Vector3.positiveInfinity) || max.Equals(Vector3.negativeInfinity))
        {
            boundingVertices = [];
            return;
        }

        boundingVertices = BoundsUtility.GetVertices(min, max);
        item.transform.InverseTransformPoints(boundingVertices);
        CalculateBounds();
    }

    public void CalculateBounds()
    {
        if (boundingVertices.Length == 0 || item == null)
        {
            bounds = new Bounds(Vector3NaN, Vector3NaN);
            return;
        }

        bounds = GeometryUtility.CalculateBounds(boundingVertices, item.transform.localToWorldMatrix);
    }

    public bool HasBounds
    {
        get
        {
            if (float.IsNaN(bounds.center.x))
                return false;
            if (float.IsNaN(bounds.center.y))
                return false;
            if (float.IsNaN(bounds.center.z))
                return false;
            if (float.IsNaN(bounds.extents.x))
                return false;
            if (float.IsNaN(bounds.extents.y))
                return false;
            if (float.IsNaN(bounds.extents.z))
                return false;
            return true;
        }
    }

    public bool IsVisible(Plane[] planes)
    {
        if (!HasBounds)
            return false;
        return Geometry.TestPlanesAABB(in planes, in bounds);
    }

    public bool IsVisible(in NativeSlice<Plane> planes)
    {
        if (!HasBounds)
            return false;
        return Geometry.TestPlanesAABB(in planes, in bounds);
    }

    public bool IsWithin(Bounds bounds)
    {
        if (!HasBounds)
            return false;
        return this.bounds.Intersects(bounds);
    }

    public bool IsWithin(TileContents tile)
    {
        return IsWithin(tile.rendererBounds);
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

    public bool Equals(GrabbableObjectContents other)
    {
        return ReferenceEquals(item, other.item);
    }

    public override bool Equals(object other)
    {
        if (other is GrabbableObjectContents otherContents)
            return Equals(otherContents);
        return false;
    }
}
