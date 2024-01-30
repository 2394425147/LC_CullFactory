using System;
using System.Collections.Generic;
using System.Diagnostics;
using CullFactory.Behaviours;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using static CullFactory.Plugin;

namespace CullFactory.Extenders;

public class RadarMapExtender
{
    public static int cachedradartarget { get; private set; }
    public static bool cachedneedsculling { get; private set; }
    public static bool NeedsCulling(int TargetIndex, bool force = false)
    {
        if (TargetIndex == cachedradartarget && !force) { return cachedneedsculling; }
        cachedradartarget = TargetIndex;
        TransformAndName Target = StartOfRound.Instance.mapScreen.radarTargets[TargetIndex];
        if (Target.transform != null) 
        {
            if (!Target.isNonPlayer)
            {
                PlayerControllerB Player = Target.transform.gameObject.GetComponentInChildren<PlayerControllerB>();
                if (Player.playerClientId != GameNetworkManager.Instance.localPlayerController.playerClientId &&
                    Player.isInsideFactory)
                {
                    Log($"Added Player {Target.name} as depth culling target");
                    cachedneedsculling = true;
                    return true;
                }
            }
            else
            {
                RadarBoosterItem Radar = Target.transform.gameObject.GetComponentInChildren<RadarBoosterItem>();
                if (Radar.isInFactory)
                {
                    Log($"Added Booster {Target.name} as depth culling target");
                    cachedneedsculling = true;
                    return true;
                }
            }
        }
        cachedneedsculling = false;
        return false;
    }
    [HarmonyPatch(typeof(PlayerControllerB), "TeleportPlayer")]
    [HarmonyPostfix]
    public static void OnTeleport()
    { NeedsCulling(cachedradartarget, true); }
}