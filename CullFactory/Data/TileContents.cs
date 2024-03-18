using DunGen;
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
    public readonly Tile tile;
    public readonly Bounds bounds;
    public readonly Renderer[] renderers;
    public readonly Light[] lights;
    public readonly int[] lightCullingMasks;

    public readonly Light[] externalLights;
    public readonly int[] externalLightCullingMasks;
    public readonly Renderer[] externalLightOccluders;

    public TileContents(
        Tile tile,
        Renderer[] renderers,
        Light[] lights,
        int[] lightCullingMasks,
        Light[] externalLights,
        int[] externalLightCullingMasks,
        Renderer[] externalLightOccluders)
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

        this.renderers = renderers;
        this.lights = lights;
        this.lightCullingMasks = lightCullingMasks;

        this.externalLights = externalLights;
        this.externalLightCullingMasks = externalLightCullingMasks;
        this.externalLightOccluders = externalLightOccluders;
    }

    private static bool _warnedNullObject = false;

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
    private void SetVisible(Light[] lights, int[] cullingMasks, bool visible)
    {
        var lightCount = lights.Length;
        for (var i = 0; i < lightCount; i++)
        {
            var light = lights[i];
            if (IsInvalid(light))
                continue;

            light.cullingMask = visible ? cullingMasks[i] : 0;
        }
    }

    public void SetVisible(bool visible)
    {
        SetVisible(renderers, visible);
        SetVisible(lights, lightCullingMasks, visible);
        SetVisible(externalLights, externalLightCullingMasks, visible);
        SetVisible(externalLightOccluders, visible);
    }
}
