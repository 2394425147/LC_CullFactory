﻿using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    public readonly struct VisibilitySets
    {
        public readonly List<TileContents> tiles = [];
        public readonly List<GrabbableObjectContents> items = [];
        public readonly List<Light> dynamicLights = [];

        public VisibilitySets()
        {
        }

        public void ClearAll()
        {
            tiles.Clear();
            items.Clear();
            dynamicLights.Clear();
        }
    }

    public static CullingMethod Instance { get; private set; }

    protected Camera _hudCamera;

    protected bool _benchmarking = false;
    protected long _totalCalls = 0;

    private float _updateInterval;
    private float _lastUpdateTime;

    private VisibilitySets _visibility = new();
    private VisibilitySets _visibilityLastCall = new();

    private double _cullingTime = 0;

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
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(false);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(false);
        DynamicObjects.AllLightsOutside.SetVisible(false);
        DynamicObjects.AllLightsInInterior.SetVisible(false);

        RenderPipelineManager.beginContextRendering += DoCulling;
    }

    internal void OnDynamicLightsCollected()
    {
        DynamicObjects.AllLightsOutside.SetVisible(false);
        DynamicObjects.AllLightsInInterior.SetVisible(false);
        _visibilityLastCall.dynamicLights.SetVisible(true);
    }

    internal void OnItemCreatedOrChanged(GrabbableObjectContents item)
    {
        bool wasVisible = false;
        for (var i = 0; i < _visibilityLastCall.items.Count; i++)
        {
            if (_visibilityLastCall.items[i].item == item.item)
            {
                _visibilityLastCall.items[i] = item;
                wasVisible = true;
            }
        }
        if (!wasVisible)
            item.SetVisible(false);
    }

    protected virtual void BenchmarkEnded()
    {
        var avgCullingTime = _cullingTime / _totalCalls;
        Plugin.Log($"Total culling time {avgCullingTime * 1000000:0.####} microseconds.");

        _cullingTime = 0;
    }

    private void Update()
    {
        if (UnityInput.Current.GetKey("LeftAlt") && UnityInput.Current.GetKeyUp("B"))
        {
            _benchmarking = !_benchmarking;

            if (!_benchmarking)
            {
                BenchmarkEnded();
                _totalCalls = 0;
            }
        }
    }

    protected abstract void AddVisibleObjects(List<Camera> cameras, VisibilitySets visibility);

    protected void AddAllObjectsWithinOrthographicCamera(Camera camera, VisibilitySets visibility)
    {
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, tileContents.bounds))
                visibility.tiles.Add(tileContents);
        }

        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
        {
            if (itemContents.IsVisible(frustum))
                visibility.items.Add(itemContents);
        }

        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsOutside)
        {
            if (itemContents.IsVisible(frustum))
                visibility.items.Add(itemContents);
        }

        visibility.dynamicLights.AddRange(DynamicObjects.AllLightsOutside);
        foreach (var interiorDynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (interiorDynamicLight.Affects(_visibility.tiles))
                visibility.dynamicLights.Add(interiorDynamicLight);
        }
    }

    private void DoCulling(ScriptableRenderContext context, List<Camera> cameras)
    {
        bool needsCulling = false;
        foreach (var camera in cameras)
        {
            if (ReferenceEquals(camera, _hudCamera))
                continue;
            // Skip the Unity editor's scene view and UnityExplorer's freecam to allow inspecting the current culling from third person.
            if (camera.name == "UE_Freecam")
                continue;
            if (camera.name == "SceneCamera")
                continue;
            needsCulling = true;
        }
        if (!needsCulling)
            return;

        if (Time.time - _lastUpdateTime < _updateInterval)
            return;

        var startTime = Time.realtimeSinceStartupAsDouble;

        _lastUpdateTime = Time.time;

        _visibility.ClearAll();

        AddVisibleObjects(cameras, _visibility);
        _totalCalls++;

        // Update culling for tiles.
        foreach (var tileContent in _visibilityLastCall.tiles)
        {
            if (!_visibility.tiles.Contains(tileContent))
                tileContent.SetVisible(false);
        }
        _visibility.tiles.SetVisible(true);

        // Update culling for items.
        _visibilityLastCall.items.Except(_visibility.items).SetVisible(false);
        _visibility.items.Except(_visibilityLastCall.items).SetVisible(true);

        // Update culling for lights.
        _visibilityLastCall.dynamicLights.Except(_visibility.dynamicLights).SetVisible(false);
        _visibility.dynamicLights.Except(_visibilityLastCall.dynamicLights).SetVisible(true);

        (_visibilityLastCall, _visibility) = (_visibility, _visibilityLastCall);

        _cullingTime += Time.realtimeSinceStartupAsDouble - startTime;
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(true);
        DynamicObjects.AllLightsOutside.SetVisible(true);
        DynamicObjects.AllLightsInInterior.SetVisible(true);

        _visibilityLastCall.ClearAll();

        RenderPipelineManager.beginContextRendering -= DoCulling;
    }

    private void OnDestroy()
    {
        Instance = null;
    }

    private void OnDrawGizmos()
    {
        if (_visibilityLastCall.tiles.Count > 0)
        {
            Gizmos.color = Color.green;
            var contents = _visibilityLastCall.tiles[0];
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
            foreach (var externalLightOccluder in contents.externalRenderers)
            {
                var bounds = externalLightOccluder.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}