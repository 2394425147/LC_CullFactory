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

    private static HashSet<GrabbableObjectContents> GrabbableObjectsToRefresh = [];

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

    internal static void MarkGrabbableObjectDirty(GrabbableObject item)
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
            AllLightsOutside.ExceptWith(contents.lights);
            AllLightsInInterior.ExceptWith(contents.lights);
        }
        else
        {
            Plugin.Log($"Adding contents of {item.name}");
            contents = new GrabbableObjectContents(item);
            GrabbableObjectToContents[item] = contents;
        }

        GrabbableObjectsToRefresh.Add(contents);
    }

    internal static void RefreshGrabbableObjects()
    {
        foreach (var contents in GrabbableObjectsToRefresh)
            RefreshGrabbableObject(contents);

        GrabbableObjectsToRefresh.Clear();
    }

    internal static void RefreshGrabbableObject(GrabbableObjectContents contents)
    {
        var item = contents.item;

        if (item == null)
            return;

        Plugin.Log($"Refreshing contents of {item.name} @ {item.transform.position}");

        // Set the item visible and remove it from the sets of outside/inside items.
        contents.SetVisible(true);

        AllLightsOutside.ExceptWith(contents.lights);
        AllLightsInInterior.ExceptWith(contents.lights);

        // GrabbableObjectContents is hashed based on the underlying GrabbableObject,
        // so if we don't remove from both sets here, we will end up with stale data
        // in these sets.
        AllGrabbableObjectContentsOutside.Remove(contents);
        AllGrabbableObjectContentsInInterior.Remove(contents);

        contents.CollectContents();

        // Notify the culling method that it must make the item invisible again if
        // it was before.
        CullingMethod.Instance?.OnItemCreatedOrChanged(contents);

        var position = item.transform.position;

        if (item.parentObject != null)
            position = item.parentObject.position;

        if (item.parentObject != null && item.parentObject.transform.TryGetComponentInParent(out EnemyAI enemy))
        {
            contents.heldByEnemy = enemy;
            if (!ItemsHeldByEnemies.TryGetValue(enemy, out var heldItems))
                ItemsHeldByEnemies.Add(enemy, heldItems = []);
            heldItems.Add(contents);
        }
        else if (contents.heldByEnemy is not null)
        {
            if (ItemsHeldByEnemies.TryGetValue(contents.heldByEnemy, out var heldItems))
                heldItems.Remove(contents);
            contents.heldByEnemy = null;
        }

        if (IsInInterior(position))
        {
            AllLightsInInterior.UnionWith(contents.lights);
            AllGrabbableObjectContentsInInterior.Add(contents);
        }
        else
        {
            AllLightsOutside.UnionWith(contents.lights);
            AllGrabbableObjectContentsOutside.Add(contents);
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

            MarkGrabbableObjectDirty(item);
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
            MarkGrabbableObjectDirty(item.item);
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
            RefreshSpecificLight(light);
    }

    internal static void RefreshSpecificLight(Light light)
    {
        if (light == null || !light.isActiveAndEnabled)
        {
            AllLightsOutside.Remove(light);
            AllLightsInInterior.Remove(light);
        }
        else if (IsInInterior(light.transform.position))
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
            MarkGrabbableObjectDirty(item);

        CullingMethod.Instance?.OnDynamicLightsCollected();
    }
}
