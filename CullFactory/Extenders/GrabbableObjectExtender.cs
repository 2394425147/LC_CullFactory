using CullFactory.Data;
using HarmonyLib;

namespace CullFactory.Extenders;

internal class GrabbableObjectExtender
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    private static void GrabbableObjectStarted(GrabbableObject __instance)
    {
        DynamicObjects.RefreshGrabbableObject(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.EnablePhysics))]
    private static void GrabbableObjectDropped(GrabbableObject __instance)
    {
        DynamicObjects.RefreshGrabbableObject(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.EnableItemMeshes))]
    private static void GrabbableObjectShownOrHidden(GrabbableObject __instance)
    {
        DynamicObjects.RefreshGrabbableObject(__instance);
    }
}
