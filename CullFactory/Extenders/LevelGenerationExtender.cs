using System;
using System.Collections.Generic;
using CullFactory.Behaviours;
using DunGen;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(RoundManager))]
public sealed class LevelGenerationExtender
{
    private static bool _cullerAdded;

    public static readonly List<TileVisibility> Tiles = new();
    private static         Random               _lastRandom;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
    private static void OnLevelGenerated()
    {
        if (_lastRandom == RoundManager.Instance.LevelRandom)
            return;

        _lastRandom = RoundManager.Instance.LevelRandom;

        if (!_cullerAdded)
        {
            StartOfRound.Instance.gameObject.gameObject.AddComponent<DynamicCuller>();
            _cullerAdded = true;
        }

        foreach (var tile in Object.FindObjectsOfType<Tile>())
        {
            Tiles.Add(new TileVisibility(tile, tile.GetComponentsInChildren<MeshRenderer>()));
        }
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
    }

    public void SetVisible(bool value)
    {
        if (_previouslyVisible == value)
            return;

        foreach (var meshRenderer in _meshRenderers)
            meshRenderer.enabled = value;

        _previouslyVisible = value;
    }
}
