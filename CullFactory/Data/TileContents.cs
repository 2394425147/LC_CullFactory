using CullFactory.Services;
using DunGen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

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
    public HDAdditionalLightData[] lightsWithOnDemandShadows;

    public Renderer[] externalRenderers = [];
    public Light[] externalLights = [];
    public Plane[][] externalLightLinesOfSight = [];
    public HDAdditionalLightData[] externalLightsWithOnDemandShadows;

    private static bool _warnedNullObject = false;

    public TileContents(Tile tile)
    {
        this.tile = tile;
        // Tile.Bounds is correct until the tile is scaled, so we have to work around some issues here.
        if (tile.OverrideAutomaticTileBounds)
        {
            // For overridden tile bounds, the sides that are set by the author will set in such a way
            // that they must have the local scale of the tile applied to be correct.
            // However, any sides that have doors will be pre-scaled, so by applying the scale we are
            // pushing them away from the origin. Therefore, we must re-condense them the same way
            // DunGen does initially.
            bounds = UnityUtil.CondenseBounds(tile.Bounds, tile.GetComponentsInChildren<Doorway>());
        }
        else
        {
            // For automatic tile bounds, all sides of the bounding box are already in pre-scaled,
            // so we just want to apply the parent transform to the bounds that have been transformed
            // into the dungeon's local space.
            bounds = tile.transform.parent.TransformBounds(tile.Placement.Bounds);
        }

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
        rendererBounds = bounds;
        foreach (var renderer in renderersList)
            rendererBounds.Encapsulate(renderer.bounds);
        var maximumBounds = bounds;
        maximumBounds.Expand(MaxRendererBoundsSizeIncreaseOverTileBounds);
        rendererBounds.min = Vector3.Max(rendererBounds.min, maximumBounds.min);
        rendererBounds.max = Vector3.Min(rendererBounds.max, maximumBounds.max);

        renderers = [.. renderersList];
        lights = [.. lightsList];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsInvalid(Component obj)
    {
        if (obj == null)
        {
            if (!_warnedNullObject)
                Plugin.LogWarning($"A {obj.GetType().Name} in {tile.name} was unexpectedly destroyed.");
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
