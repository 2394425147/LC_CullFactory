using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using static CullFactory.Plugin;

namespace CullFactory.Extenders;

public class RadarMapExtender : MonoBehaviour
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
                Log($"IsInside {Player.isInsideFactory}");
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
    public static void OnTeleport(PlayerControllerB __instance)
    {
        NeedsCulling(cachedradartarget, true);
       // __instance.StartCoroutine(checkculling());
    }
    
   /*public static IEnumerator checkculling()
    {
        yield return new WaitForSeconds(0.5f); //Waiting before checking
        NeedsCulling(cachedradartarget, true); //Forcing function to recheck current target
    }*/
}