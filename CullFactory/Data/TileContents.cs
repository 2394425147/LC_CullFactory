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
public sealed class TileContents(
    Tile tile,
    Renderer[] renderers,
    Light[] lights,
    int[] lightCullingMasks,
    Light[] externalLights,
    int[] externalLightCullingMasks,
    Renderer[] externalLightOccluders)
{
    public readonly Tile tile = tile;
    public readonly Bounds bounds = tile.OverrideAutomaticTileBounds
                                        ? tile.Bounds
                                        : tile.transform.parent.TransformBounds(tile.Placement.Bounds);
    public readonly Renderer[] renderers = renderers;
    public readonly Light[] lights = lights;
    public readonly int[] lightCullingMasks = lightCullingMasks;

    public readonly Light[] externalLights = externalLights;
    public readonly int[] externalLightCullingMasks = externalLightCullingMasks;
    public readonly Renderer[] externalLightOccluders = externalLightOccluders;

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
