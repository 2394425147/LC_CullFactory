using HarmonyLib;
using System.Globalization;

namespace CullFactory.Extenders;

internal class MapSeedOverride
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPostfix]
    private static void StartOfRound_StartPostfix(StartOfRound __instance)
    {
        __instance.overrideRandomSeed = int.TryParse(Config.OverrideMapSeed.Value, NumberStyles.Any,
                                                     CultureInfo.InvariantCulture, out var seed);

        if (__instance.overrideRandomSeed)
            __instance.overrideSeedNumber = seed;
    }
}
