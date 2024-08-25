using CullFactory.Behaviours.CullingMethods;
using CullFactory.Services;
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

    public static Dictionary<EnemyAI, HashSet<GrabbableObjectContents>> ItemsHeldByEnemies = [];

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
        if (DungeonCullingInfo.AllTileContents == null)
            return false;
        if (DungeonCullingInfo.DungeonBounds.Contains(position))
            return true;
        return false;
    }

    internal static bool PlayerIsInInterior(PlayerControllerB player)
    {
        return IsInInterior(player.transform.position);
    }

    internal static void RefreshGrabbableObject(GrabbableObject item)
    {
        // If mods flag an item with DontSave, we won't find it in FindObjectsByType(),
        // so don't include it here to avoid ever culling it.
        if (item.hideFlags.HasFlag(HideFlags.DontSave))
            return;
        // Apparently Unity lets us find some objects that aren't in one of the visible
        // scenes, so prefabs that some mods create will end up here, let's filter those
        // out so that we don't have to do any processing on them.
        if (!item.gameObject.scene.isLoaded)
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
        if (item.parentObject != null && item.parentObject.transform.TryGetComponentInParent(out EnemyAI enemy))
        {
            isInInterior = !enemy.isOutside;

            contents.heldByEnemy = enemy;
            if (!ItemsHeldByEnemies.TryGetValue(enemy, out var heldItems))
                ItemsHeldByEnemies.Add(enemy, heldItems = []);
            heldItems.Add(contents);
        }
        else
        {
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
                isInInterior = PlayerIsInInterior(item.playerHeldBy);
            }

            if (contents.heldByEnemy is not null)
            {
                if (ItemsHeldByEnemies.TryGetValue(contents.heldByEnemy, out var heldItems))
                    heldItems.Remove(contents);
                contents.heldByEnemy = null;
            }
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
        // PlayerControllerB.isInsideFactory may not be accurate if an error occurs while
        // teleporting, so let's just check their position instead.
        if (PlayerIsInInterior(player))
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

    internal static void OnEnemyTeleported(EnemyAI enemy)
    {
        if (!ItemsHeldByEnemies.TryGetValue(enemy, out var items))
            return;
        foreach (var item in items)
        {
            if (item.item == null)
                continue;
            RefreshGrabbableObject(item.item);
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

        AllUnpredictableLights.UnionWith(StartOfRound.Instance.mapScreen.mapCamera.GetComponentsInChildren<Light>());

        var imperiumMap = GameObject.Find("ImpMap");
        if (imperiumMap != null)
            AllUnpredictableLights.UnionWith(imperiumMap.GetComponentsInChildren<Light>());

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

        ItemsHeldByEnemies.Clear();

        CollectAllLightsInWorld();

        CollectAllUnpredictableLights();

        foreach (var player in StartOfRound.Instance.allPlayerScripts)
            OnPlayerTeleported(player);

        foreach (var item in UnityEngine.Object.FindObjectsByType<GrabbableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            RefreshGrabbableObject(item);

        CullingMethod.Instance?.OnDynamicLightsCollected();
    }
}
