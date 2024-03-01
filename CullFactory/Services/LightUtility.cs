using CullFactory.Data;
using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Services
{
    public static class LightUtility
    {
        public static bool Affects(this Light light, Bounds bounds)
        {
            var lightRange = light.range;
            var lightDistanceSquared = bounds.SqrDistance(light.transform.position);
            if (lightDistanceSquared > lightRange * lightRange)
                return false;
            // TODO: Take into account light shapes other than point.
            return true;
        }

        public static bool Affects(this Light light, TileContents tile)
        {
            return light.Affects(tile.bounds);
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
    }
}
