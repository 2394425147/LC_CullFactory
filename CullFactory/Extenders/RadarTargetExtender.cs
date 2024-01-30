using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using static CullFactory.Plugin;

namespace CullFactory.Extenders;

public class RadarTargetExtender : MonoBehaviour
{
    public static int cachedradartarget { get; private set; }
    public static bool cachedneedsculling { get; private set; }
    //Gets called every DynamicCuller.Update() tick
    public static bool NeedsCulling(int TargetIndex, bool force = false)
    {
        //Current radar target gets cached and updated only if target changed or forced update on PlayerTeleport event
        if (TargetIndex == cachedradartarget && !force) { return cachedneedsculling; } 
        //Expensive calculations ahead
        cachedradartarget = TargetIndex;
        TransformAndName Target = StartOfRound.Instance.mapScreen.radarTargets[TargetIndex];
        if (Target.transform != null) //Safety measures
        {
            if (!Target.isNonPlayer) //If target is Player
            {
                PlayerControllerB Player = Target.transform.gameObject.GetComponentInChildren<PlayerControllerB>();
                Log($"IsInside {Player.isInsideFactory}");
                if (Player.playerClientId != GameNetworkManager.Instance.localPlayerController.playerClientId &&
                    Player.isInsideFactory) //Check if player is not localplayer and if player inside the factory
                {
                    //Result gets cached, target being culled
                    Log($"Added Player {Target.name} as depth culling target");
                    cachedneedsculling = true;
                    return true;
                }
            }
            else //If target is Radar
            {
                RadarBoosterItem Radar = Target.transform.gameObject.GetComponentInChildren<RadarBoosterItem>();
                if (Radar.isInFactory) //If Radar is in factory (Doesn't work with EnhancedRadarBooster mod sadly ;-;)
                {
                    //Result gets cached, target being culled
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
    public static void OnTeleport(PlayerControllerB __instance)
    { NeedsCulling(cachedradartarget, true); } //Force update culling on target if someone teleports in/out of building
}