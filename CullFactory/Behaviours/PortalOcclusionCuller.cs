using CullFactory.Data;
using DunGen;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours;

public sealed class PortalOcclusionCuller : MonoBehaviour
{
    private readonly List<TileContents> _visibleTiles = new();

    private GameObject _portalVisualizersRoot;

    private void OnEnable()
    {
        HideTileContents(DungeonCullingInfo.AllTileContents);

        RenderPipelineManager.beginCameraRendering += CullForCamera;

        SpawnAllPortalVisualizers();
    }

    private static void HideTileContents(IEnumerable<TileContents> tileContents)
    {
        foreach (var tile in tileContents)
        {
            foreach (var light in tile.externalLights)
                light.enabled = false;
            foreach (var renderer in tile.externalLightOccluders)
                renderer.forceRenderingOff = true;
        }

        foreach (var tile in tileContents)
        {
            foreach (var renderer in tile.renderers)
                renderer.forceRenderingOff = true;
            foreach (var light in tile.lights)
                light.enabled = false;
        }
    }

    private static void ShowTileContents(IEnumerable<TileContents> tileContents)
    {
        foreach (var tile in tileContents)
        {
            foreach (var light in tile.externalLights)
                light.enabled = true;
            foreach (var renderer in tile.externalLightOccluders)
            {
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }
        foreach (var tile in tileContents)
        {
            foreach (var renderer in tile.renderers)
            {
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            foreach (var light in tile.lights)
                light.enabled = true;
        }
    }

    private void CollectVisibleTiles(Camera fromCamera, List<TileContents> intoList)
    {
        if (fromCamera.orthographic)
        {
            var frustum = GeometryUtility.CalculateFrustumPlanes(fromCamera);

            foreach (var tileContents in DungeonCullingInfo.AllTileContents)
            {
                if (GeometryUtility.TestPlanesAABB(frustum, tileContents.bounds))
                    intoList.Add(tileContents);
            }
            return;
        }

        var currentTileContents = fromCamera.transform.position.GetTileContents();

        if (currentTileContents != null)
            DungeonCullingInfo.CallForEachLineOfSight(fromCamera, currentTileContents.tile, (tiles, index) => intoList.Add(DungeonCullingInfo.TileContentsForTile[tiles[index]]));
    }

    private void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
            return;

        HideTileContents(_visibleTiles);
        _visibleTiles.Clear();

        CollectVisibleTiles(camera, _visibleTiles);
        ShowTileContents(_visibleTiles);
    }

    private void SpawnPortalVisualizer(GameObject prefab, Doorway doorway)
    {
        var portalVisualizer = Instantiate(prefab, _portalVisualizersRoot.transform);
        portalVisualizer.transform.position = doorway.transform.position;
        portalVisualizer.transform.transform.rotation = doorway.transform.rotation;
        portalVisualizer.transform.localScale = new Vector3(doorway.Socket.Size.x, doorway.Socket.Size.y, 1);
    }

    private void SpawnAllPortalVisualizers()
    {
        if (!Plugin.Configuration.VisualizePortals.Value)
            return;

        if (_portalVisualizersRoot != null)
            Destroy(_portalVisualizersRoot);
        _portalVisualizersRoot = new GameObject("PortalVisualizers");

        var portalPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portalPrefab.name = "PortalVisualizer";

        // Destroy is deferred, so destroy the collider immediately to avoid cloning it
        // when using this as a prefab.
        DestroyImmediate(portalPrefab.GetComponent<Collider>());

        var material = new Material(Shader.Find("HDRP/Unlit"))
        {
            name = "PortalVisualizerMaterial",
            color = Color.gray,
        };
        portalPrefab.GetComponent<Renderer>().material = material;

        var mesh = portalPrefab.GetComponent<MeshFilter>().mesh;
        // Mesh.vertices elements cannot be modified directly.
        var vertices = mesh.vertices;
        var vertexOffset = new Vector3(0, 0.5f, -Plugin.Configuration.VisualizedPortalOutsetDistance.Value);
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = vertices[i] + vertexOffset;
        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        foreach (var doorwayConnection in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.Connections)
        {
            SpawnPortalVisualizer(portalPrefab, doorwayConnection.A);
            SpawnPortalVisualizer(portalPrefab, doorwayConnection.B);
        }

        Destroy(portalPrefab);
    }

    private void OnDisable()
    {
        ShowTileContents(DungeonCullingInfo.AllTileContents);

        RenderPipelineManager.beginCameraRendering -= CullForCamera;

        Destroy(_portalVisualizersRoot);
    }
}
