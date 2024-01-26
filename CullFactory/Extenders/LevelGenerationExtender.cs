using System;
using System.Collections.Generic;
using CullFactory.Behaviours;
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
            MeshContainers.Add(tile, new TileVisibility(tile, tile.GetComponentsInChildren<MeshRenderer>()));

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<DynamicCuller>();
    }
}

public class TileVisibility
{
    public readonly  Tile           parentTile;
    private readonly MeshRenderer[] _meshRenderers;

    private bool _previouslyVisible = true;

    public TileVisibility(Tile parentTile, MeshRenderer[] meshRenderers)
    {
        this.parentTile = parentTile;
        _meshRenderers  = Array.FindAll(meshRenderers, renderer => renderer.enabled);

        if (Plugin.Configuration.Logging.Value)
            Plugin.Log($"Found tile {parentTile.name} with {meshRenderers.Length} mesh renderers");
    }

    public void SetVisible(bool value)
    {
        if (_previouslyVisible == value)
            return;

        if (Plugin.Configuration.Logging.Value)
            Plugin.Log(value ? $"Showing {parentTile.name}" : $"Culling {parentTile.name}");

        foreach (var meshRenderer in _meshRenderers)
            meshRenderer.enabled = value;

        _previouslyVisible = value;
    }
}
