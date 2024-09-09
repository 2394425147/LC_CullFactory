using CullFactory.Data;
using HarmonyLib;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(GrabbableObject))]
internal static class GrabbableObjectExtender
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GrabbableObject.Start))]
    private static void GrabbableObjectStarted(GrabbableObject __instance)
    {
        DynamicObjects.MarkGrabbableObjectDirty(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GrabbableObject.EnablePhysics))]
    private static void GrabbableObjectDropped(GrabbableObject __instance)
    {
        DynamicObjects.MarkGrabbableObjectDirty(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GrabbableObject.EnableItemMeshes))]
    private static void GrabbableObjectShownOrHidden(GrabbableObject __instance)
    {
        DynamicObjects.MarkGrabbableObjectDirty(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GrabbableObject.GrabItemFromEnemy))]
    private static void GrabbableObjectPickedUpByEnemy(GrabbableObject __instance)
    {
        DynamicObjects.MarkGrabbableObjectDirty(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GrabbableObject.DiscardItemFromEnemy))]
    private static void GrabbableObjectDroppedByEnemy(GrabbableObject __instance)
    {
        DynamicObjects.MarkGrabbableObjectDirty(__instance);
    }
}
