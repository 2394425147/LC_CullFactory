using HarmonyLib;
using System.Globalization;

namespace CullFactory.Extenders;

internal class MapSeedOverride
{
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    [HarmonyPostfix]
    private static void StartOfRound_StartPostfix(StartOfRound __instance)
    {
        if (int.TryParse(Plugin.Configuration.OverrideMapSeed.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var seed))
        {
            __instance.overrideRandomSeed = true;
            __instance.overrideSeedNumber = seed;
        }
        else
        {
            __instance.overrideRandomSeed = false;
        }
    }
}
