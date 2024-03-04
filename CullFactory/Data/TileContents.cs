using CullFactory.Services;
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
    Bounds bounds,
    Renderer[] renderers,
    Light[] lights,
    Light[] externalLights,
    Renderer[] externalLightOccluders,
    Plane[][] externalLightLinesOfSight)
{
    public readonly Tile tile = tile;
    public readonly Bounds bounds = bounds;
    public readonly Renderer[] renderers = renderers;
    public readonly Light[] lights = lights;

    public readonly Light[] externalLights = externalLights;
    public readonly Renderer[] externalLightOccluders = externalLightOccluders;
    public readonly Plane[][] externalLightLinesOfSight = externalLightLinesOfSight;

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

    public void SetVisible(bool visible)
    {
        SetVisible(renderers, visible);
        SetVisible(lights, visible);
        SetVisible(externalLights, visible);
        SetVisible(externalLightOccluders, visible);
    }

    public override string ToString()
    {
        if (tile == null)
            return "Destroyed";
        return tile.name;
    }
}
