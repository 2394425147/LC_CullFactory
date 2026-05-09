using CullFactory.Data;
using CullFactoryBurst;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Services;

public static class LightUtility
{
    public const float ExtraShadowFadeDistance = 1 / 0.9f;

    public static bool Affects(this Light light, Bounds bounds)
    {
        var lightRange = light.range;
        var lightDistanceSquared = bounds.SqrDistance(light.transform.position);
        if (lightDistanceSquared > lightRange * lightRange)
            return false;
        if (light.type == LightType.Spot)
            return Geometry.SpotLightInfluencesBounds(light, bounds);
        return true;
    }

    public static bool Affects(this Light light, TileContents tile)
    {
        return light.Affects(tile.rendererBounds);
    }

    public static bool Affects(this Light light, IEnumerable<TileContents> tiles)
    {
        foreach (var tile in tiles)
        {
            if (light.Affects(tile))
                return true;
        }
        return false;
    }

    public static bool HasShadows(this Light light)
    {
        return light.shadows != LightShadows.None;
    }

    public static bool PassesThroughOccluders(this Light light)
    {
        return !light.HasShadows() || light.GetComponent<HDAdditionalLightData>() is { shadowDimmer: < 1 };
    }

    public static float GetMinimumShadowFadeDistance(float lightFadeDistance)
    {
        return lightFadeDistance * ExtraShadowFadeDistance;
    }

    public static bool ShouldDisableShadowFading(this HDAdditionalLightData light)
    {
        // A heuristic to determine whether a light is "close enough" to fading the shadows
        // at the same distance as the light itself. These values are arbitrarily chosen to
        // return true for all lights in the mansion interior, where the performance impact
        // is negligible.
        // They may need adjustment if this results in a significant performance regression
        // in any other interiors.
        return light.shadowFadeDistance >= light.fadeDistance * 0.75 - 15;
    }

    public static bool ShadowFadingAllowsLeakage(this HDAdditionalLightData light)
    {
        if (Config.DisableShadowDistanceFading.Value)
            return !light.ShouldDisableShadowFading();
        return light.shadowFadeDistance + 0.001f < GetMinimumShadowFadeDistance(light.fadeDistance);
    }

    public static bool ShadowFadingAllowsLeakage(this Light light)
    {
        if (light.GetComponent<HDAdditionalLightData>() is { } hdLight)
            return hdLight.ShadowFadingAllowsLeakage();

        return false;
    }

    public static void SetVisible(this Light light, bool visible)
    {
        if (light == null)
            return;

        if (light.cullingMask != -1 && light.cullingMask != 0)
            Plugin.LogWarning($"Light {light.name}'s culling mask was an unexpected value of {light.cullingMask}.");

        light.cullingMask = visible ? -1 : 0;
    }

    public static void SetVisible(this IEnumerable<Light> lights, bool visible)
    {
        foreach (var light in lights)
            light.SetVisible(visible);
    }
}
