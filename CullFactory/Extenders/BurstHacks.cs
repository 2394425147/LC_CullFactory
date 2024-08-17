using HarmonyLib;
using Unity.Burst;

namespace CullFactory.Extenders;

internal static class BurstHacks
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PreInitSceneScript), nameof(PreInitSceneScript.ChooseLaunchOption))]
    public static void ChooseLaunchOptionPrefix(bool online)
    {
        // Workaround for https://github.com/2394425147/LC_CullFactory/issues/17
        // Disable Burst when connecting to a host in LAN to avoid a crash.
        if (!online)
        {
            BurstCompiler.Options.EnableBurstCompilation = false;
            Plugin.LogAlways("Disabled Burst compilation to prevent a crash when joining a LAN host.");
        }
    }
}
