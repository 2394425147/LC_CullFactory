using System;
using System.Collections.Generic;
using BepInEx;
using CullFactory.Behaviours.API;
using CullFactory.Data;
using CullFactory.Services;
using CullFactoryBurst;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    public readonly struct VisibilitySets
    {
        public readonly HashSet<TileContents> directTiles = new(IdentityEqualityComparer<TileContents>.Instance);
        public readonly HashSet<TileContents> indirectTiles = new(IdentityEqualityComparer<TileContents>.Instance);
        public readonly HashSet<GrabbableObjectContents> items = new(EqualityComparer<GrabbableObjectContents>.Default);
        public readonly HashSet<Light> dynamicLights = new(IdentityEqualityComparer<Light>.Instance);
        public readonly HashSet<HDAdditionalLightData> onDemandShadowedLights = new(IdentityEqualityComparer<HDAdditionalLightData>.Instance);

        public VisibilitySets()
        {
        }

        public void ClearAll()
        {
            directTiles.Clear();
            indirectTiles.Clear();
            items.Clear();
            dynamicLights.Clear();
            onDemandShadowedLights.Clear();
        }
    }

    public const float ExtraShadowFadeDistance = 1 / 0.9f;

    public static CullingMethod Instance { get; private set; }

    protected Camera _hudCamera;
    protected LayerMask _onDemandShadowMapCullingMask = LayerMask.GetMask("Room", "MiscLevelGeometry", "Terrain");

    protected bool _benchmarking = false;
    protected long _totalCalls = 0;

    private float _updateInterval;
    private float _lastUpdateTime;

    private bool _renderedThisFrame = false;

    private List<Camera> _camerasToCullThisPass = [];

    private VisibilitySets _visibility = new();
    private VisibilitySets _visibilityLastCall = new();

    private float[] _lightShadowFadeDistances;
    private Dictionary<LODGroup, float> _lastLODScreenHeights;

    protected TileContents _debugTile = null;

    private double _cullingTime = 0;

    public static void Initialize()
    {
        if (Instance != null)
        {
            DestroyImmediate(Instance);
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

        // Get the camera for the HUD/UI via an object referenced by HUDManager so that
        // if the UICamera's hierarchy is modified (i.e. by LethalCompanyVR), we can still
        // find the camera.
        var hudContainerTransform = HUDManager.Instance.HUDContainer.transform;
        var hudCanvas = hudContainerTransform.GetComponentInParent<Canvas>();
        if (hudCanvas != null)
            _hudCamera = hudCanvas.worldCamera;
        else
            Plugin.LogWarning("Failed to find the HUD canvas, culling will be calculated unnecessarily for the HUD camera.");
    }

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetSelfVisible(false);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(false);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(false);
        DynamicObjects.AllLightsOutside.SetVisible(false);
        DynamicObjects.AllLightsInInterior.SetVisible(false);

        DisableShadowDistanceFading();
        DisableInteriorLODCulling();

        RenderPipelineManager.beginContextRendering += DoCullingScriptableRenderPipeline;
        Camera.onPreCull += DoCullingInStandardRenderPipeline;
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

    private void LateUpdate()
    {
        _renderedThisFrame = false;
    }

    protected abstract void AddVisibleObjects(List<Camera> cameras, VisibilitySets visibility);

    protected void AddAllObjectsWithinOrthographicCamera(Camera camera, VisibilitySets visibility)
    {
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
        {
            if (Geometry.TestPlanesAABB(frustum, tileContents.bounds))
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

    protected void AddAllObjects(VisibilitySets visibility)
    {
        visibility.directTiles.UnionWith(DungeonCullingInfo.AllTileContents);
        visibility.items.UnionWith(DynamicObjects.AllGrabbableObjectContentsInInterior);
        visibility.items.UnionWith(DynamicObjects.AllGrabbableObjectContentsOutside);
        visibility.dynamicLights.UnionWith(DynamicObjects.AllLightsInInterior);
        visibility.dynamicLights.UnionWith(DynamicObjects.AllLightsOutside);
    }

    private static readonly List<Camera> _singleCamera = [];

    private void DoCullingInStandardRenderPipeline(Camera camera)
    {
        _singleCamera.Clear();
        _singleCamera.Add(camera);
        DoCulling(_singleCamera);
    }

    private void DoCullingScriptableRenderPipeline(ScriptableRenderContext context, List<Camera> cameras)
    {
        DoCulling(cameras);
    }

    private void DoCulling(List<Camera> cameras)
    {
        _camerasToCullThisPass.Clear();
        bool anyCameraDisablesCulling = false;

        foreach (var camera in cameras)
        {
            if (ReferenceEquals(camera, _hudCamera))
                continue;
            // Skip the Unity editor's scene view and UnityExplorer's freecam to allow inspecting the current culling from third person.
            if (camera.name == "UE_Freecam")
                continue;
            if (camera.name == "SceneCamera")
                continue;
            var options = camera.GetComponent<CameraCullingOptions>();
            if (options != null)
            {
                if (options.SkipCulling)
                    continue;
                if (options.DisableCulling)
                    anyCameraDisablesCulling = true;
            }

            _camerasToCullThisPass.Add(camera);
        }

        if (_camerasToCullThisPass.Count == 0)
            return;

        if (Time.time - _lastUpdateTime < _updateInterval)
            return;

        if (!_renderedThisFrame)
        {
            DynamicObjects.RefreshGrabbableObjects();
            DynamicObjects.UpdateAllUnpredictableLights();
            _renderedThisFrame = true;
        }

        var startTime = Time.realtimeSinceStartupAsDouble;

        _lastUpdateTime = Time.time;

        _visibility.ClearAll();
        _debugTile = null;

        if (anyCameraDisablesCulling)
            AddAllObjects(_visibility);
        else
            AddVisibleObjects(_camerasToCullThisPass, _visibility);

        // Collect the lights with on-demand shadow maps for this frame.
        foreach (var visibleTile in _visibility.directTiles)
        {
            foreach (var light in visibleTile.lightsWithOnDemandShadows)
            {
                if (!light.isActiveAndEnabled)
                    continue;
                _visibility.onDemandShadowedLights.Add(light);
            }
            foreach (var light in visibleTile.externalLightsWithOnDemandShadows)
            {
                if (!light.isActiveAndEnabled)
                    continue;
                _visibility.onDemandShadowedLights.Add(light);
            }
        }

        // Reset the light culling masks for any lights we already rendered on-demand shadow maps for,
        // then clear the maps that we are no longer using.
        foreach (var light in _visibility.onDemandShadowedLights)
        {
            if (light != null && _visibilityLastCall.onDemandShadowedLights.Contains(light))
                ResetCullingMask(light);
        }
        foreach (var light in _visibilityLastCall.onDemandShadowedLights)
        {
            if (light != null && !_visibility.onDemandShadowedLights.Contains(light))
                EvictOnDemandShadowMap(light);
        }

        // Update culling for tiles.
        bool removedAnyTile = false;

        foreach (var tileContent in _visibilityLastCall.directTiles)
        {
            if (!_visibility.directTiles.Contains(tileContent))
            {
                tileContent.SetSelfVisible(false);
                tileContent.SetExternalInfluencesVisible(false);
                removedAnyTile = true;
            }
        }
        foreach (var tileContent in _visibilityLastCall.indirectTiles)
        {
            if (!_visibility.indirectTiles.Contains(tileContent))
            {
                tileContent.SetRenderersVisible(false);
                removedAnyTile = true;
            }
        }

        foreach (var tileContent in _visibility.directTiles)
        {
            if (removedAnyTile || !_visibilityLastCall.directTiles.Contains(tileContent))
            {
                tileContent.SetSelfVisible(true);
                tileContent.SetExternalInfluencesVisible(true);
            }
        }
        foreach (var tileContent in _visibility.indirectTiles)
        {
            if (removedAnyTile || !_visibilityLastCall.indirectTiles.Contains(tileContent))
                tileContent.SetRenderersVisible(true);
        }

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

        // Set up any new lights with on-demand shadow maps to be rendered this frame.
        foreach (var light in _visibility.onDemandShadowedLights)
        {
            if (light != null && !_visibilityLastCall.onDemandShadowedLights.Contains(light))
                SetCullingMaskAndRenderOnDemandShadowMap(light);
        }

        (_visibilityLastCall, _visibility) = (_visibility, _visibilityLastCall);

        if (_benchmarking)
        {
            _totalCalls++;
            _cullingTime += Time.realtimeSinceStartupAsDouble - startTime;
        }
    }

    private void ResetCullingMask(HDAdditionalLightData light)
    {
        light.SetCullingMask(-1);
    }

    private void SetCullingMaskAndRenderOnDemandShadowMap(HDAdditionalLightData light)
    {
        light.SetCullingMask(_onDemandShadowMapCullingMask);
        light.RequestShadowMapRendering();
    }

    private void EvictOnDemandShadowMap(HDAdditionalLightData light)
    {
        // Lights that are having their shadow maps evicted are invisible.
        // They will not have their mask set by ResetCullingMask, so reset it here.
        light.SetCullingMask(0);

        HDCachedShadowManager.instance.ForceEvictLight(light);
    }

    private void OnDisable()
    {
        foreach (var light in _visibilityLastCall.onDemandShadowedLights)
            EvictOnDemandShadowMap(light);

        DungeonCullingInfo.AllTileContents.SetSelfVisible(true);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(true);
        DynamicObjects.AllLightsOutside.SetVisible(true);
        DynamicObjects.AllLightsInInterior.SetVisible(true);

        _visibilityLastCall.ClearAll();

        RestoreShadowDistanceFading();
        RestoreInteriorLODCulling();

        RenderPipelineManager.beginContextRendering -= DoCullingScriptableRenderPipeline;
        Camera.onPreCull -= DoCullingInStandardRenderPipeline;
    }

    private void OnDestroy()
    {
        Instance = null;
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

            if (!DungeonCullingInfo.ShouldShadowFadingBeDisabledForLight(hdLight))
                continue;
            hdLight.shadowFadeDistance = hdLight.fadeDistance * ExtraShadowFadeDistance;
        }
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

    private void DisableInteriorLODCulling()
    {
        if (!Config.DisableLODCulling.Value)
            return;

        _lastLODScreenHeights = [];

        foreach (var tile in DungeonCullingInfo.AllTileContents)
        {
            foreach (var lodGroup in tile.tile.GetComponentsInChildren<LODGroup>())
            {
                var lods = lodGroup.GetLODs();
                _lastLODScreenHeights[lodGroup] = lods[^1].screenRelativeTransitionHeight;
                lods[^1].screenRelativeTransitionHeight = 0;
                lodGroup.SetLODs(lods);
            }
        }
    }

    private void RestoreInteriorLODCulling()
    {
        if (_lastLODScreenHeights == null)
            return;

        foreach (var tile in DungeonCullingInfo.AllTileContents)
        {
            foreach (var lodGroup in tile.tile.GetComponentsInChildren<LODGroup>())
            {
                if (!_lastLODScreenHeights.TryGetValue(lodGroup, out var screenRelativeTransitionHeight))
                    continue;
                var lods = lodGroup.GetLODs();
                lods[^1].screenRelativeTransitionHeight = screenRelativeTransitionHeight;
                lodGroup.SetLODs(lods);
            }
        }

        _lastLODScreenHeights = null;
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