using System.Collections.Generic;
using BepInEx;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static List<TileContents> _visibleTiles = [];
    private static List<TileContents> _visibleTilesLastCall = [];

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);

        RenderPipelineManager.beginCameraRendering += CullForCamera;

        _visibleTiles.Clear();
    }

    private void Update()
    {
        if (!_benchmarkStarted)
            _benchmarkStarted = UnityInput.Current.GetKeyDown(KeyCode.B);
    }

    private static double _totalTime;
    private static int _callCount;
    private static bool _benchmarkStarted;

    private static void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
            return;

        var startTime = Time.realtimeSinceStartupAsDouble;

        _visibleTiles.Clear();
        _visibleTiles.AddContentsVisibleToCamera(camera);

        foreach (var tileContent in _visibleTilesLastCall)
            tileContent.SetVisible(_visibleTiles.Contains(tileContent));

        _visibleTiles.SetVisible(true);

        (_visibleTilesLastCall, _visibleTiles) = (_visibleTiles, _visibleTilesLastCall);

        if (!_benchmarkStarted || _callCount > 10000)
            return;

        _callCount++;
        _totalTime += Time.realtimeSinceStartupAsDouble - startTime;
        Debug.Log($"{_totalTime * 1000:0.0000}ms\t({_callCount})\t({_totalTime / _callCount:0.0000}ms)");
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
