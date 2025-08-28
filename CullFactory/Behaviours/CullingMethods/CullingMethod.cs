using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using CullFactory.Behaviours.API;
using CullFactory.Behaviours.Visualization;
using CullFactory.Data;
using CullFactory.Services;
using CullFactoryBurst;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    public struct VisibilitySets
    {
        public readonly HashSet<TileContents> directTiles = new(IdentityEqualityComparer<TileContents>.Instance);
        public readonly HashSet<TileContents> indirectTiles = new(IdentityEqualityComparer<TileContents>.Instance);
        public readonly HashSet<GrabbableObjectContents> items = new(EqualityComparer<GrabbableObjectContents>.Default);
        public readonly HashSet<Light> dynamicLights = new(IdentityEqualityComparer<Light>.Instance);

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

    public const float ExtraShadowFadeDistance = 1 / 0.9f;

    private static GameObject CullingObject;

    public static CullingMethod Instance { get; private set; }

    protected Camera _hudCamera;

    protected bool _benchmarking = false;
    protected long _totalCalls = 0;

    private float _updateInterval;
    private float _lastUpdateTime;

    private bool _renderedThisFrame = false;

    private List<Camera> _camerasToCullThisPass = [];

    private VisibilitySets _visibility = new();
    private VisibilitySets _visibilityLastCall = new();

    private ConditionalWeakTable<Dungeon, float[]> _lightShadowFadeDistances = [];
    private Dictionary<LODGroup, float> _lastLODScreenHeights = [];

    protected TileContents _debugTile = null;

    private float _cullingTime = 0;

    public static bool GetContainer(out GameObject containerObject)
    {
        containerObject = CullingObject;
        if (containerObject != null)
            return true;

        if (StartOfRound.Instance == null)
            return false;
        var parent = StartOfRound.Instance.transform.parent;

        var obj = new GameObject("Culling");
        obj.transform.SetParent(parent, false);
        CullingObject = containerObject = obj;
        return true;
    }

    public static void Initialize()
    {
        if (!GetContainer(out var container))
            return;

        CullingVisualizer.Initialize();

        if (Instance != null)
        {
            DestroyImmediate(Instance);
            Instance = null;
        }

        var level = StartOfRound.Instance.currentLevel;
        if (level == null || !Config.ShouldEnableCullingForScene(level.sceneName))
            return;

        var instance = Config.GetCullingType() switch
        {
            CullingType.PortalOcclusionCulling => container.AddComponent<PortalOcclusionCuller>(),
            CullingType.DepthCulling => container.AddComponent<DepthCuller>(),
            _ => (CullingMethod)null
        };

        if (instance == null)
            return;

        if (Config.UpdateFrequency.Value > 0)
            instance._updateInterval = 1 / Config.UpdateFrequency.Value;
        else
            instance._updateInterval = 0;

        Instance = instance;

    }

    private void Awake()
    {
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
        DungeonCullingInfo.SetAllTileContentsVisible(false);
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
        var frustum = camera.GetTempFrustum();

        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];
            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (Geometry.TestPlanesAABB(frustum, tileContents.bounds))
                    visibility.directTiles.Add(tileContents);
            }
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
        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
            visibility.directTiles.UnionWith(DungeonCullingInfo.AllDungeonData[i].AllTileContents);
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

    protected float GetProfileTime()
    {
        if (!_benchmarking)
            return 0;
        return Time.realtimeSinceStartup;
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
            var cameraName = camera.name;
            if (cameraName == "UE_Freecam")
                continue;
            if (cameraName == "SceneCamera")
                continue;
            if (camera.TryGetComponent(out CameraCullingOptions options))
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

        var updateTime = Time.time;
        if (updateTime - _lastUpdateTime < _updateInterval)
            return;
        _lastUpdateTime = updateTime;

        if (!_renderedThisFrame)
        {
            DynamicObjects.RefreshGrabbableObjects();
            DynamicObjects.UpdateAllUnpredictableLights();
            _renderedThisFrame = true;
        }

        var startTime = GetProfileTime();

        _visibility.ClearAll();
        _debugTile = null;

        if (anyCameraDisablesCulling)
            AddAllObjects(_visibility);
        else
            AddVisibleObjects(_camerasToCullThisPass, _visibility);

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

        (_visibilityLastCall, _visibility) = (_visibility, _visibilityLastCall);

        if (_benchmarking)
        {
            _totalCalls++;
            _cullingTime += GetProfileTime() - startTime;
        }
    }

    private void OnDisable()
    {
        DungeonCullingInfo.SetAllTileContentsVisible(true);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(true);
        DynamicObjects.AllLightsOutside.SetVisible(true);
        DynamicObjects.AllLightsInInterior.SetVisible(true);

        _visibilityLastCall.ClearAll();

        RestoreShadowDistanceFading();
        RestoreInteriorLODCulling();

        RenderPipelineManager.beginContextRendering -= DoCullingScriptableRenderPipeline;
        Camera.onPreCull -= DoCullingInStandardRenderPipeline;

        DungeonCullingInfo.CleanUpDestroyedDungeons();
    }

    private void OnDestroy()
    {
        Instance = null;
    }

    private void DisableShadowDistanceFading()
    {
        RestoreShadowDistanceFading();

        if (!Config.DisableShadowDistanceFading.Value)
            return;

        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];
            if (!dungeonData.DungeonRef.TryGetTarget(out var dungeon))
                continue;

            var distances = new float[dungeonData.AllLightsInDungeon.Length];
            _lightShadowFadeDistances.Add(dungeon, distances);

            for (var j = 0; j < distances.Length; j++)
            {
                var light = dungeonData.AllLightsInDungeon[j];
                if (light == null)
                    continue;
                var hdLight = light.GetComponent<HDAdditionalLightData>();
                if (hdLight == null)
                    continue;

                distances[j] = hdLight.shadowFadeDistance;

                if (!DungeonCullingInfo.ShouldShadowFadingBeDisabledForLight(hdLight))
                    continue;
                hdLight.shadowFadeDistance = hdLight.fadeDistance * ExtraShadowFadeDistance;
            }
        }
    }

    private void RestoreShadowDistanceFading()
    {
        if (_lightShadowFadeDistances == null)
            return;

        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];
            if (!dungeonData.DungeonRef.TryGetTarget(out var dungeon))
                continue;
            if (!_lightShadowFadeDistances.TryGetValue(dungeon, out var distances))
                continue;

            for (var j = 0; j < distances.Length; j++)
            {
                var light = dungeonData.AllLightsInDungeon[j];
                if (light == null)
                    continue;
                var hdLight = light.GetComponent<HDAdditionalLightData>();
                if (hdLight == null)
                    continue;

                hdLight.shadowFadeDistance = distances[j];
            }
        }

        _lightShadowFadeDistances.Clear();
    }

    private void DisableInteriorLODCulling()
    {
        RestoreInteriorLODCulling();

        if (!Config.DisableLODCulling.Value)
            return;

        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];

            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (tileContents.tile == null)
                    continue;
                foreach (var lodGroup in tileContents.tile.GetComponentsInChildren<LODGroup>())
                {
                    var lods = lodGroup.GetLODs();
                    _lastLODScreenHeights[lodGroup] = lods[^1].screenRelativeTransitionHeight;
                    lods[^1].screenRelativeTransitionHeight = 0;
                    lodGroup.SetLODs(lods);
                }
            }
        }
    }

    private void RestoreInteriorLODCulling()
    {
        if (_lastLODScreenHeights == null)
            return;

        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];

            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (tileContents.tile == null)
                    continue;
                foreach (var lodGroup in tileContents.tile.GetComponentsInChildren<LODGroup>())
                {
                    if (!_lastLODScreenHeights.TryGetValue(lodGroup, out var screenRelativeTransitionHeight))
                        continue;
                    var lods = lodGroup.GetLODs();
                    lods[^1].screenRelativeTransitionHeight = screenRelativeTransitionHeight;
                    lodGroup.SetLODs(lods);
                }
            }
        }

        _lastLODScreenHeights.Clear();
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