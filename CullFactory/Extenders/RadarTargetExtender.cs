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
        //Safety measures
        if (Target.transform != null)
        {   //If target is Player
            if (!Target.isNonPlayer) 
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
            //If target is Radar
            else 
            {
                RadarBoosterItem Radar = Target.transform.gameObject.GetComponentInChildren<RadarBoosterItem>();
                //If Radar is in factory (Doesn't work with EnhancedRadarBooster mod sadly ;-;)
                if (Radar.isInFactory) 
                {
                    //Result gets cached, target being culled
                    Log($"Added Booster {Target.name} as depth culling target");
                    cachedneedsculling = true;
                    return true;
                }
            }
        }
        //Target doesn't need to be culled, results are cached
        cachedneedsculling = false;
        return false;
    }

    [HarmonyPatch(typeof(PlayerControllerB), "TeleportPlayer")]
    [HarmonyPostfix]
    public static void OnTeleport(PlayerControllerB __instance)
    {
        //Force update culling on target if someone teleports in/out of building
        __instance.StartCoroutine(WaitBeforeCheck()); 
    }

    public static IEnumerator WaitBeforeCheck()
    {
        //Waiting 250ms after player teleport event for InFacility property to update
        yield return new WaitForSeconds(0.25f); 
        NeedsCulling(cachedradartarget, true); 
    }
}