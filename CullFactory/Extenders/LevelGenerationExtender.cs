﻿using System;
using System.Collections.Generic;
using CullFactory.Behaviours;
using DunGen;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

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

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<DynamicCuller>();
    }
}

public class TileVisibility
{
    public readonly Tile parentTile;

    private readonly MeshRenderer[] _meshRenderers;
    private readonly Light[]        _lights;

    private bool _previouslyVisible = true;

    public TileVisibility(Tile parentTile)
    {
        this.parentTile = parentTile;

        _meshRenderers = Array.FindAll(parentTile.GetComponentsInChildren<MeshRenderer>(), renderer => renderer.enabled);
        _lights        = Array.FindAll(parentTile.GetComponentsInChildren<Light>(),        renderer => renderer.enabled);

        foreach (var lighting in parentTile.GetComponentsInChildren<HDAdditionalLightData>())
            lighting.affectsVolumetric = false;

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
