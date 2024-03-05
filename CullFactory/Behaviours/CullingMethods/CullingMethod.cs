using System;
using System.Collections.Generic;
using System.Linq;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    public static CullingMethod Instance { get; private set; }

    protected Camera _hudCamera;

    private float _updateInterval;
    private float _lastUpdateTime;

    private List<TileContents> _visibleTiles = [];
    private List<TileContents> _visibleTilesLastCall = [];

    private List<GrabbableObjectContents> _visibleItems = [];
    private List<GrabbableObjectContents> _visibleItemsLastCall = [];

    private List<Light> _visibleDynamicLights = [];
    private List<Light> _visibleDynamicLightsLastCall = [];

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
        _visibleDynamicLightsLastCall.SetVisible(true);
    }

    internal void OnItemCreatedOrChanged(GrabbableObjectContents item)
    {
        bool wasVisible = false;
        for (var i = 0; i < _visibleItemsLastCall.Count; i++)
        {
            if (_visibleItemsLastCall[i].item == item.item)
            {
                _visibleItemsLastCall[i] = item;
                wasVisible = true;
            }
        }
        if (!wasVisible)
            item.SetVisible(false);
    }

    protected abstract void AddVisibleObjects(List<Camera> cameras, List<TileContents> visibleTiles, List<GrabbableObjectContents> visibleItems, List<Light> visibleDynamicLights);

    protected void AddAllObjectsWithinOrthographicCamera(Camera camera, List<TileContents> visibleTiles, List<GrabbableObjectContents> visibleItems, List<Light> visibleDynamicLights)
    {
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, tileContents.bounds))
                visibleTiles.Add(tileContents);
        }

        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
        {
            if (itemContents.IsVisible(frustum))
                visibleItems.Add(itemContents);
        }

        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsOutside)
        {
            if (itemContents.IsVisible(frustum))
                visibleItems.Add(itemContents);
        }

        visibleDynamicLights.AddRange(DynamicObjects.AllLightsOutside);
        foreach (var interiorDynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (interiorDynamicLight.Affects(visibleTiles))
                visibleDynamicLights.Add(interiorDynamicLight);
        }
    }

    private void DoCulling(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (cameras.Count == 1)
        {
            var theCamera = cameras[0];
            if (ReferenceEquals(theCamera, _hudCamera))
                return;
            // Skip the Unity editor's scene view and UnityExplorer's freecam to allow inspecting the current culling from third person.
            if (theCamera.name == "UE_Freecam")
                return;
            if (theCamera.name == "SceneCamera")
                return;
        }

        if (Time.time - _lastUpdateTime < _updateInterval)
            return;

        _lastUpdateTime = Time.time;

        _visibleTiles.Clear();
        _visibleItems.Clear();
        _visibleDynamicLights.Clear();
        AddVisibleObjects(cameras, _visibleTiles, _visibleItems, _visibleDynamicLights);

        // Update culling for tiles.
        foreach (var tileContent in _visibleTilesLastCall)
        {
            if (!_visibleTiles.Contains(tileContent))
                tileContent.SetVisible(false);
        }
        _visibleTiles.SetVisible(true);

        // Update culling for items.
        _visibleItemsLastCall.Except(_visibleItems).SetVisible(false);
        _visibleItems.Except(_visibleItemsLastCall).SetVisible(true);

        // Update culling for lights.
        _visibleDynamicLightsLastCall.Except(_visibleDynamicLights).SetVisible(false);
        _visibleDynamicLights.Except(_visibleDynamicLightsLastCall).SetVisible(true);

        (_visibleTilesLastCall, _visibleTiles) = (_visibleTiles, _visibleTilesLastCall);
        (_visibleItemsLastCall, _visibleItems) = (_visibleItems, _visibleItemsLastCall);
        (_visibleDynamicLightsLastCall, _visibleDynamicLights) = (_visibleDynamicLights, _visibleDynamicLightsLastCall);
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsOutside.SetVisible(true);
        DynamicObjects.AllGrabbableObjectContentsInInterior.SetVisible(true);
        DynamicObjects.AllLightsOutside.SetVisible(true);
        DynamicObjects.AllLightsInInterior.SetVisible(true);

        _visibleTilesLastCall.Clear();
        _visibleItemsLastCall.Clear();
        _visibleDynamicLightsLastCall.Clear();

        RenderPipelineManager.beginContextRendering -= DoCulling;
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