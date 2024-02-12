using System.Linq;
using CullFactory.Data;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.Visualization
{
    internal class CullingVisualizer : MonoBehaviour
    {
        private const float TileBoundsInset = 0.00025f;

        private static readonly Shader VisualizerShader = Shader.Find("HDRP/Unlit");
        private static readonly Color[] ColorRotation = [Color.red, Color.yellow, Color.green, Color.blue, Color.cyan, Color.grey];

        private static readonly Material PortalVisualizerMaterial = new(VisualizerShader)
        {
            name = "PortalVisualizerMaterial",
            color = Color.white,
        };

        private static readonly Material TileBoundsVisualizerMaterial = new(VisualizerShader)
        {
            name = "TileBoundsVisualizerMaterial",
            color = Color.yellow,
        };

        private GameObject _portalVisualizersRoot;
        private GameObject _tileBoundsVisualizersRoot;

        private void OnEnable()
        {
            RefreshVisualizers();
        }

        public void RefreshVisualizers()
        {
            SpawnPortalVisualizers();
            SpawnTileBoundsVisualizers();
        }

        private void SpawnPortalVisualizer(Transform prefab, Doorway doorway)
        {
            var portalVisualizer = Instantiate(prefab, _portalVisualizersRoot.transform);
            portalVisualizer.position = doorway.transform.position;
            portalVisualizer.rotation = doorway.transform.rotation;
            portalVisualizer.localScale = new Vector3(doorway.Socket.Size.x, doorway.Socket.Size.y, 1);
        }

        private void SpawnPortalVisualizers()
        {
            Destroy(_portalVisualizersRoot);
            if (!Config.VisualizePortals.Value)
                return;
            _portalVisualizersRoot = new GameObject("PortalVisualizers");

            var portalPrefab = CreatePortalVisualizer();

            foreach (var doorwayConnection in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.Connections)
            {
                SpawnPortalVisualizer(portalPrefab, doorwayConnection.A);
                SpawnPortalVisualizer(portalPrefab, doorwayConnection.B);
            }

            Destroy(portalPrefab);
        }

        private void SpawnTileBoundsVisualizers()
        {
            Destroy(_tileBoundsVisualizersRoot);
            if (!Config.VisualizeTileBounds.Value)
                return;
            _tileBoundsVisualizersRoot = new GameObject("TileBoundsVisualizers");

            var tileBoundsPrefab = CreateTileBoundsVisualizer();

            var insetVector = new Vector3(TileBoundsInset, TileBoundsInset, TileBoundsInset);

            for (var i = 0; i < DungeonCullingInfo.AllTileContents.Length; i++)
            {
                var tile = DungeonCullingInfo.AllTileContents[i];
                var tileBoundsVisualizer = Instantiate(tileBoundsPrefab, _tileBoundsVisualizersRoot.transform);
                tileBoundsVisualizer.transform.position = tile.bounds.center;
                tileBoundsVisualizer.transform.localScale = tile.bounds.size - insetVector;
                tileBoundsVisualizer.material.color = ColorRotation[i % ColorRotation.Length];
            }

            Destroy(tileBoundsPrefab);
        }

        private static Transform CreatePortalVisualizer()
        {
            var portalPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portalPrefab.name = "PortalVisualizer";

            // Destroy is deferred, so destroy the collider immediately to avoid cloning it
            // when using this as a prefab.
            DestroyImmediate(portalPrefab.GetComponent<Collider>());

            var renderer = portalPrefab.GetComponent<Renderer>();
            renderer.material = PortalVisualizerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var mesh = portalPrefab.GetComponent<MeshFilter>().mesh;
            // Mesh.vertices elements cannot be modified directly.
            var vertices = mesh.vertices;
            var vertexOffset = new Vector3(0, 0.5f, -Config.VisualizedPortalOutsetDistance.Value);
            for (var i = 0; i < vertices.Length; i++)
                vertices[i] += vertexOffset;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return portalPrefab.transform;
        }

        private static Renderer CreateTileBoundsVisualizer()
        {
            var tileBoundsPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileBoundsPrefab.name = "TileBounds";

            DestroyImmediate(tileBoundsPrefab.GetComponent<Collider>());

            var renderer = tileBoundsPrefab.GetComponent<Renderer>();
            renderer.material = TileBoundsVisualizerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var mesh = tileBoundsPrefab.GetComponent<MeshFilter>().sharedMesh;
            mesh.triangles = [.. mesh.triangles, .. mesh.triangles.Reverse()];

            return renderer;
        }

        private void OnDisable()
        {
            Destroy(_portalVisualizersRoot);
            Destroy(_tileBoundsVisualizersRoot);
        }
    }
}
