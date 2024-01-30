using System;
using System.Collections.Generic;
using System.Diagnostics;
using CullFactory.Behaviours;
using HarmonyLib;
using UnityEngine;
using static CullFactory.Plugin;
namespace CullFactory.Extenders;
public class RadarMapExtender
{
    [HarmonyPatch(typeof(ManualCameraRenderer), "updateMapTarget")]
    [HarmonyPostfix]
    private static void OnTargetSwitch(ref int setRadarTargetIndex, ManualCameraRenderer __instance)
    {
       // __instance.radarTargets[1].isNonPlayer
       if (!__instance.radarTargets[__instance.targetTransformIndex].isNonPlayer)
       {
           Log(__instance.radarTargets[__instance.targetTransformIndex].transform.gameObject.ToString());
       }
       else
       {
           Log(__instance.radarTargets[__instance.targetTransformIndex].transform.gameObject.ToString());
       }
    }
    [HarmonyPatch(typeof(ManualCameraRenderer), "Update")]
    [HarmonyPostfix]
    private static void OnUpdate(ManualCameraRenderer __instance)
    {
        // __instance.radarTargets[1].isNonPlayer
        //this.radarTargets[this.targetTransformIndex].transform
        //Log(__instance.radarTargets[__instance.targetTransformIndex].transform.ToString());
    }
}