using System;
using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    public static CullingMethod Instance { get; private set; }

    protected Camera _hudCamera;

    private float _updateInterval;
    private float _lastUpdateTime;

    private List<TileContents> _visibleTiles = [];
    private List<TileContents> _visibleTilesLastCall = [];

    public static void Initialize()
    {
        if (Instance != null)
        {
            Destroy(Instance);
            Instance = null;
        }

        if (RoundManager.Instance == null || RoundManager.Instance.dungeonGenerator == null)
            return;

        var generator = RoundManager.Instance.dungeonGenerator.Generator;
        var dungeon = generator.CurrentDungeon.gameObject;
        CullingMethod instance = null;

        switch (Config.GetCullingType(generator.DungeonFlow))
        {
            case CullingType.PortalOcclusionCulling:
                instance = dungeon.AddComponent<PortalOcclusionCuller>();
                break;
            case CullingType.DepthCulling:
                instance = dungeon.AddComponent<DepthCuller>();
                break;
            case CullingType.None:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (instance == null)
            return;

        if (Config.UpdateFrequency.Value > 0)
            instance._updateInterval = 1 / Config.UpdateFrequency.Value;
        else
            instance._updateInterval = 0;
    }

    private void Awake()
    {
        Instance = this;

        _hudCamera = GameObject.Find("Systems/UI/UICamera").GetComponent<Camera>();
    }

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);
    }

    protected abstract void AddVisibleTiles(List<TileContents> visibleTiles);

    private void LateUpdate()
    {
        if (Time.time - _lastUpdateTime < _updateInterval)
            return;

        _lastUpdateTime = Time.time;

        _visibleTiles.Clear();
        AddVisibleTiles(_visibleTiles);

        foreach (var tileContent in _visibleTilesLastCall)
        {
            if (!_visibleTiles.Contains(tileContent))
                tileContent.SetVisible(false);
        }

        _visibleTiles.SetVisible(true);

        (_visibleTilesLastCall, _visibleTiles) = (_visibleTiles, _visibleTilesLastCall);
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
    }

    private void OnDestroy()
    {
        Instance = null;
    }

    private void OnDrawGizmos()
    {
        if (_visibleTilesLastCall.Count > 0)
        {
            Gizmos.color = Color.green;
            var contents = _visibleTilesLastCall[0];
            Gizmos.DrawWireCube(contents.bounds.center, contents.bounds.size);

            Gizmos.color = Color.green;
            foreach (var renderer in contents.renderers)
            {
                var bounds = renderer.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            foreach (var light in contents.lights)
                Gizmos.DrawWireSphere(light.transform.position, light.range);

            Gizmos.color = Color.blue;
            foreach (var externalLight in contents.externalLights)
                Gizmos.DrawWireSphere(externalLight.transform.position, externalLight.range);
            foreach (var externalLightOccluder in contents.externalLightOccluders)
            {
                var bounds = externalLightOccluder.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}