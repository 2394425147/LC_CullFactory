using CullFactory.Services;
using DunGen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CullFactory.Data;

/// <summary>
///     Stores the contents of a DunGen Tile relevant to culling it from the view.
///     Note that none of the fields in this class are guaranteed to be children of
///     the Tile they are associated with. Children of doors are shared between the
///     both neighboring Tiles.
/// </summary>
public sealed class TileContents
{
    private const float MaxRendererBoundsSizeIncreaseOverTileBounds = 40f;

    public readonly Tile tile;
    public readonly Bounds bounds;
    public Portal[] portals;

    public readonly Bounds rendererBounds;
    public Renderer[] renderers;
    public readonly Light[] lights;

    public Renderer[] externalRenderers = [];
    public Light[] externalLights = [];
    public Plane[][] externalLightLinesOfSight = [];

    private static bool _warnedNullObject = false;

    public TileContents(Tile tile)
    {
        this.tile = tile;

        var renderersList = new List<Renderer>(tile.GetComponentsInChildren<Renderer>(includeInactive: true));
        var lightsList = new List<Light>(tile.GetComponentsInChildren<Light>(includeInactive: true));

        // Get the doors that this tile is connected to. Otherwise, they may pop in and out when the edge of the view
        // frustum is at the edge of the portal.
        foreach (var doorway in tile.UsedDoorways)
        {
            if (doorway.doorComponent == null)
                continue;
            renderersList.AddRange(doorway.GetComponentsInChildren<Renderer>(includeInactive: true));
            lightsList.AddRange(doorway.GetComponentsInChildren<Light>(includeInactive: true));
        }

        // Create a bounding box that encompasses everything that may be accessible around the tile.
        // Some interiors like to make entry tiles that have bounds that have upper areas that have a
        // wider accessible area than the tile bounds indicate, including the vanilla mineshaft.
        // We have an upper limit on the size of these to prevent stray renderers from causing the
        // any part of the exterior from being considered part of the interior.
        rendererBounds = new();
        foreach (var renderer in renderersList)
        {
            if (rendererBounds.extents.Equals(Vector3.zero))
                rendererBounds = renderer.bounds;
            else
                rendererBounds.Encapsulate(renderer.bounds);
        }

        // The calculation of overridden tile bounds was incorrect in the version of DunGen that Lethal
        // Company shipped prior to v80. As a heuristic to choose the right way of calculating the exact
        // bounds, select the candidate that has the least volume when encapsulating the renderer bounds.
        Bounds mostRecentBounds = tile.transform.TransformBounds(tile.Placement.LocalBounds);
        Bounds slightlyMoreRecentBounds = tile.transform.parent.TransformBounds(tile.Placement.Bounds);
        bounds = SelectBestBounds(mostRecentBounds, slightlyMoreRecentBounds, rendererBounds);

        if (tile.OverrideAutomaticTileBounds)
        {
            var oldBounds = tile.transform.TransformBounds(tile.TileBoundsOverride);
            oldBounds = UnityUtil.CondenseBounds(oldBounds, tile.AllDoorways);
            bounds = SelectBestBounds(bounds, oldBounds, rendererBounds);
        }

        bounds = UnityUtil.CondenseBounds(bounds, tile.AllDoorways);

        var maximumBounds = bounds;
        maximumBounds.Expand(MaxRendererBoundsSizeIncreaseOverTileBounds);
        rendererBounds.min = Vector3.Max(rendererBounds.min, maximumBounds.min);
        rendererBounds.max = Vector3.Min(rendererBounds.max, maximumBounds.max);

        renderers = [.. renderersList];
        lights = [.. lightsList];
    }

    private static Bounds SelectBestBounds(Bounds a, Bounds b, Bounds rendererBounds)
    {
        return VolumeOfEncapsulation(a, rendererBounds) <= VolumeOfEncapsulation(b, rendererBounds) ? a : b;
    }

    private static float VolumeOfEncapsulation(Bounds a, Bounds b)
    {
        var encapsulation = a;
        encapsulation.Encapsulate(b);
        var size = encapsulation.size;
        return size.x * size.y * size.z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInvalid(Component obj)
    {
        if (obj == null)
        {
            if (!_warnedNullObject)
                Plugin.LogWarning($"A {obj.GetType().Name} in {this} was unexpectedly destroyed.");
            _warnedNullObject = true;

            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetVisible(Renderer[] renderers, bool visible)
    {
        foreach (var renderer in renderers)
        {
            if (IsInvalid(renderer))
                continue;

            renderer.forceRenderingOff = !visible;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetVisible(Light[] lights, bool visible)
    {
        var lightCount = lights.Length;
        for (var i = 0; i < lightCount; i++)
        {
            var light = lights[i];
            if (IsInvalid(light))
                continue;

            light.SetVisible(visible);
        }
    }

    public void SetRenderersVisible(bool visible)
    {
        SetVisible(renderers, visible);
    }

    public void SetLightsVisible(bool visible)
    {
        SetVisible(lights, visible);
    }

    public void SetSelfVisible(bool visible)
    {
        SetRenderersVisible(visible);
        SetLightsVisible(visible);
    }

    public void SetExternalInfluencesVisible(bool visible)
    {
        SetVisible(externalRenderers, visible);
        SetVisible(externalLights, visible);
    }

    public override string ToString()
    {
        if (tile == null)
            return "Destroyed";
        return tile.name;
    }
}
