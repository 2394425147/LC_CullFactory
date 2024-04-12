using CullFactory.Behaviours.CullingMethods;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CullFactory.Data;

public static class DynamicObjects
{
    public static readonly HashSet<Light> AllLightsOutside = [];
    public static readonly HashSet<Light> AllLightsInInterior = [];
    public static readonly HashSet<Light> AllUnpredictableLights = [];

    public static HashSet<GrabbableObjectContents> AllGrabbableObjectContentsOutside = [];
    public static HashSet<GrabbableObjectContents> AllGrabbableObjectContentsInInterior = [];
    public static Dictionary<GrabbableObject, GrabbableObjectContents> GrabbableObjectToContents = [];

    private static Light[][] allPlayerLights;

    internal static void CollectAllPlayerLights()
    {
        var players = StartOfRound.Instance.allPlayerScripts;
        var playerCount = players.Length;
        allPlayerLights = new Light[playerCount][];

        for (var i = 0; i < playerCount; i++)
        {
            // This is only necessary because MoreCompany calls TeleportPlayer() before all the expanded lobby
            // is instantiated.
            if (players[i] == null)
                continue;
            allPlayerLights[i] = players[i].GetComponentsInChildren<Light>(includeInactive: true);
        }
    }

    internal static bool IsInInterior(Vector3 position)
    {
        if (DungeonCullingInfo.DungeonBounds.SqrDistance(position) <= DungeonCullingInfo.SqrOutsideTileRadius)
            return true;
        return false;
    }

    internal static void RefreshGrabbableObject(GrabbableObject item)
    {
        // If mods flag an item with DontSave, we won't find it in FindObjectsByType(),
        // so don't include it here to avoid ever culling it.
        if (item.hideFlags.HasFlag(HideFlags.DontSave))
            return;
        if (GrabbableObjectToContents.TryGetValue(item, out var contents))
        {
            Plugin.Log($"Refreshing contents of {item.name}");
            AllLightsOutside.ExceptWith(contents.lights);
            AllLightsInInterior.ExceptWith(contents.lights);

            contents.CollectContents();
        }
        else
        {
            Plugin.Log($"Adding contents of {item.name}");
            contents = new GrabbableObjectContents(item);
            GrabbableObjectToContents[item] = contents;
        }

        bool isInInterior;
        if (item.playerHeldBy is null)
        {
            // GrabbableObject.isInFactory is not reliable for items that are in the ship
            // at the start of the game.
            if (item.GetComponentInChildren<ScanNodeProperties>() is ScanNodeProperties scanNode)
                isInInterior = IsInInterior(scanNode.transform.position);
            else
                isInInterior = IsInInterior(item.transform.position);
        }
        else
        {
            // Items may be within the bounds of the dungeon when held by a player.
            isInInterior = item.playerHeldBy.isInsideFactory;
        }

        if (isInInterior)
        {
            AllLightsOutside.ExceptWith(contents.lights);
            AllLightsInInterior.UnionWith(contents.lights);

            AllGrabbableObjectContentsOutside.Remove(contents);
            AllGrabbableObjectContentsInInterior.Add(contents);
        }
        else
        {
            AllLightsOutside.UnionWith(contents.lights);
            AllLightsInInterior.ExceptWith(contents.lights);

            AllGrabbableObjectContentsOutside.Add(contents);
            AllGrabbableObjectContentsInInterior.Remove(contents);
        }
    }

    internal static void OnPlayerTeleported(PlayerControllerB player)
    {
        // This function will be called at the start of the game, so
        // populate the arrays of lights for all players.
        CollectAllPlayerLights();

        var playerIndex = Array.IndexOf(StartOfRound.Instance.allPlayerScripts, player);
        if (playerIndex == -1)
            return;

        var playerLights = allPlayerLights[playerIndex];
        if (player.isInsideFactory)
        {
            AllLightsOutside.ExceptWith(playerLights);
            AllLightsInInterior.UnionWith(playerLights);
        }
        else
        {
            AllLightsOutside.UnionWith(playerLights);
            AllLightsInInterior.ExceptWith(playerLights);
        }

        foreach (var item in player.ItemSlots)
        {
            if (item == null)
                continue;

            RefreshGrabbableObject(item);
        }
    }

    internal static void CollectAllLightsInWorld()
    {
        var allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).AsEnumerable();
        if (DungeonCullingInfo.AllLightsInDungeon != null)
            allLights = allLights.Except(DungeonCullingInfo.AllLightsInDungeon);
        AllLightsOutside.UnionWith(allLights.Where(light => !DungeonCullingInfo.DungeonBounds.Contains(light.transform.position)));
        AllLightsInInterior.UnionWith(allLights.Except(AllLightsOutside));
    }

    internal static void CollectAllUnpredictableLights()
    {
        AllUnpredictableLights.Clear();

        if (StartOfRound.Instance.spectateCamera.TryGetComponent<Light>(out var light))
            AllUnpredictableLights.Add(light);

        UpdateAllUnpredictableLights();
    }

    internal static void UpdateAllUnpredictableLights()
    {
        foreach (var light in AllUnpredictableLights)
        {
            if (light == null)
                continue;
            if (!light.isActiveAndEnabled)
                continue;
            if (IsInInterior(light.transform.position))
            {
                AllLightsOutside.Remove(light);
                AllLightsInInterior.Add(light);
            }
            else
            {
                AllLightsInInterior.Remove(light);
                AllLightsOutside.Add(light);
            }
        }
    }

    internal static void CollectAllTrackedObjects()
    {
        AllLightsOutside.Clear();
        AllLightsInInterior.Clear();

        AllGrabbableObjectContentsOutside.Clear();
        AllGrabbableObjectContentsInInterior.Clear();
        GrabbableObjectToContents.Clear();

        CollectAllLightsInWorld();

        CollectAllUnpredictableLights();

        foreach (var player in StartOfRound.Instance.allPlayerScripts)
            OnPlayerTeleported(player);

        foreach (var item in UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            RefreshGrabbableObject(item);

        CullingMethod.Instance?.OnDynamicLightsCollected();
    }
}
