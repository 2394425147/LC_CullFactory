using System;
using System.Collections.Generic;
using BepInEx;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    public struct VisibilitySets
    {
        public readonly HashSet<TileContents> directTiles = [];
        public readonly HashSet<TileContents> indirectTiles = [];
        public readonly HashSet<GrabbableObjectContents> items = [];
        public readonly HashSet<Light> dynamicLights = [];

        public VisibilitySets()
        {
        }

        public void ClearAll()
        {
            directTiles.Clear();
            indirectTiles.Clear();
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

    private float[] _lightShadowFadeDistances;

    protected TileContents _debugTile = null;

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

        switch (Config.GetCullingType(generator.DungeonFlow))
        {
            case CullingType.PortalOcclusionCulling:
                Instance = dungeon.AddComponent<PortalOcclusionCuller>();
                break;
            case CullingType.DepthCulling:
                Instance = dungeon.AddComponent<DepthCuller>();
                break;
            case CullingType.None:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (Instance == null)
            return;

        if (Config.UpdateFrequency.Value > 0)
            Instance._updateInterval = 1 / Config.UpdateFrequency.Value;
        else
            Instance._updateInterval = 0;
    }

    private void Awake()
    {
        _hudCamera = GameObject.Find("Systems/UI/UICamera").GetComponent<Camera>();
    }

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetSelfVisible(false);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(false);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(false);
        DynamicObjects.AllLightsOutside.SetVisible(false);
        DynamicObjects.AllLightsInInterior.SetVisible(false);

        DisableShadowDistanceFading();

        RenderPipelineManager.beginContextRendering += DoCulling;
    }

    private void DisableShadowDistanceFading()
    {
        if (!Config.DisableShadowDistanceFading.Value)
            return;

        _lightShadowFadeDistances = new float[DungeonCullingInfo.AllLightsInDungeon.Length];
        for (var i = 0; i < _lightShadowFadeDistances.Length; i++)
        {
            var light = DungeonCullingInfo.AllLightsInDungeon[i];
            if (light == null)
                continue;
            var hdLight = light.GetComponent<HDAdditionalLightData>();
            if (hdLight == null)
                continue;

            _lightShadowFadeDistances[i] = hdLight.shadowFadeDistance;
            hdLight.shadowFadeDistance = hdLight.fadeDistance;
        }
    }

    internal void OnDynamicLightsCollected()
    {
        DynamicObjects.AllLightsOutside.SetVisible(false);
        DynamicObjects.AllLightsInInterior.SetVisible(false);
        _visibilityLastCall.dynamicLights.SetVisible(true);
    }

    internal void OnItemCreatedOrChanged(GrabbableObjectContents item)
    {
        item.SetVisible(_visibilityLastCall.items.Contains(item));
    }

    protected virtual void BenchmarkEnded()
    {
        var avgCullingTime = _cullingTime / _totalCalls;
        Plugin.Log($"Total culling time {avgCullingTime * 1000000:0.####} microseconds.");

        _cullingTime = 0;
    }

    private void Update()
    {
        if (!Config.Logging.Value)
            return;

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
                visibility.directTiles.Add(tileContents);
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

        visibility.dynamicLights.UnionWith(DynamicObjects.AllLightsOutside);
        foreach (var interiorDynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (interiorDynamicLight == null)
                continue;
            if (interiorDynamicLight.Affects(_visibility.directTiles))
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
        _debugTile = null;

        AddVisibleObjects(cameras, _visibility);

        // Update culling for tiles.
        foreach (var tileContent in _visibilityLastCall.directTiles)
        {
            if (!_visibility.directTiles.Contains(tileContent))
            {
                tileContent.SetSelfVisible(false);
                tileContent.SetExternalInfluencesVisible(false);
            }
        }
        foreach (var tileContent in _visibilityLastCall.indirectTiles)
        {
            if (!_visibility.indirectTiles.Contains(tileContent))
                tileContent.SetRenderersVisible(false);
        }

        foreach (var tileContent in _visibility.directTiles)
        {
            tileContent.SetSelfVisible(true);
            tileContent.SetExternalInfluencesVisible(true);
        }
        foreach (var tileContent in _visibility.indirectTiles)
            tileContent.SetRenderersVisible(true);

        // Update culling for items.
        foreach (var item in _visibilityLastCall.items)
        {
            if (!_visibility.items.Contains(item))
                item.SetVisible(false);
        }
        foreach (var item in _visibility.items)
        {
            if (!_visibilityLastCall.items.Contains(item))
                item.SetVisible(true);
        }

        // Update culling for lights.
        foreach (var light in _visibilityLastCall.dynamicLights)
        {
            if (light != null && !_visibility.dynamicLights.Contains(light))
                light.SetVisible(false);
        }
        foreach (var light in _visibility.dynamicLights)
        {
            if (light != null && !_visibilityLastCall.dynamicLights.Contains(light))
                light.SetVisible(true);
        }

        (_visibilityLastCall, _visibility) = (_visibility, _visibilityLastCall);

        if (_benchmarking)
        {
            _totalCalls++;
            _cullingTime += Time.realtimeSinceStartupAsDouble - startTime;
        }
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetSelfVisible(true);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(true);
        DynamicObjects.AllLightsOutside.SetVisible(true);
        DynamicObjects.AllLightsInInterior.SetVisible(true);

        _visibilityLastCall.ClearAll();

        RestoreShadowDistanceFading();

        RenderPipelineManager.beginContextRendering -= DoCulling;
    }

    private void RestoreShadowDistanceFading()
    {
        if (_lightShadowFadeDistances == null)
            return;

        for (var i = 0; i < _lightShadowFadeDistances.Length; i++)
        {
            var light = DungeonCullingInfo.AllLightsInDungeon[i];
            if (light == null)
                continue;
            var hdLight = light.GetComponent<HDAdditionalLightData>();
            if (hdLight == null)
                continue;

            hdLight.shadowFadeDistance = _lightShadowFadeDistances[i];
        }

        _lightShadowFadeDistances = null;
    }

    private void OnDrawGizmos()
    {
        if (_debugTile is not null)
        {
            Gizmos.color = Color.green;
            var contents = _debugTile;
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