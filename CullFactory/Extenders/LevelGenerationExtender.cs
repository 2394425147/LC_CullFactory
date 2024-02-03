using System;
using System.Collections.Generic;
using CullFactory.Behaviours;
using CullFactory.Data;
using DunGen;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(RoundManager))]
public sealed class LevelGenerationExtender
{
    public static readonly Dictionary<Tile, TileVisibility> MeshContainers = new();

    // Using string instead of nameof here since waitForMainEntranceTeleportToSpawn is a private method
    [HarmonyPostfix]
    [HarmonyPatch("waitForMainEntranceTeleportToSpawn")]
    private static void OnLevelGenerated()
    {
        MeshContainers.Clear();

        foreach (var tile in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles)
        {
            if (tile.UsedDoorways.FindIndex(doorway => doorway == null) != -1)
            {
                Plugin.LogError($"Tile {tile.name} has a doorway that connects nowhere, skipping");
                continue;
            }

            MeshContainers.Add(tile, new TileVisibility(tile));
        }

        DungeonCullingInfo.OnLevelGenerated();

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<DynamicCuller>();
    }
}

public class TileVisibility
{
    private readonly Light[] _lights;

    private readonly MeshRenderer[] _meshRenderers;
    public readonly Tile parentTile;

    private bool _previouslyVisible = true;

    public TileVisibility(Tile parentTile)
    {
        this.parentTile = parentTile;

        _meshRenderers = Array.FindAll(parentTile.GetComponentsInChildren<MeshRenderer>(), renderer => renderer.enabled);
        _lights = Array.FindAll(parentTile.GetComponentsInChildren<Light>(), renderer => renderer.enabled);

        Plugin.Log($"Found tile {parentTile.name} with {_meshRenderers.Length} mesh renderers and {_lights.Length} lights");
    }

    public void SetVisible(bool value)
    {
        if (_previouslyVisible == value)
            return;

        Plugin.Log(value ? $"Showing {parentTile.name}" : $"Culling {parentTile.name}");

        foreach (var meshRenderer in _meshRenderers)
            meshRenderer.enabled = value;

        foreach (var light in _lights)
            light.enabled = value;

        _previouslyVisible = value;
    }
}