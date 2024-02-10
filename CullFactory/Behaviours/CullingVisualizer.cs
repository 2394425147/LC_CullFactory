using CullFactory.Data;
using DunGen;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours
{
    internal class CullingVisualizer : MonoBehaviour
    {
        private static readonly Color[] ColorRotation = [Color.red, Color.yellow, Color.green, Color.blue, Color.cyan, Color.grey];
        private static readonly float TileBoundsInset = 0.00025f;

        private GameObject _portalVisualizersRoot;
        private GameObject _tileBoundsVisualizersRoot;

        private void OnEnable()
        {
            RefreshVisualizers();
        }

        public void RefreshVisualizers()
        {
            SpawnAllPortalVisualizers();
            SpawnAllTileBoundsVisualizers();
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
            Destroy(_portalVisualizersRoot);
            if (!Plugin.Configuration.VisualizePortals.Value)
                return;
            _portalVisualizersRoot = new GameObject("PortalVisualizers");

            var portalPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portalPrefab.name = "PortalVisualizer";

            // Destroy is deferred, so destroy the collider immediately to avoid cloning it
            // when using this as a prefab.
            DestroyImmediate(portalPrefab.GetComponent<Collider>());

            var renderer = portalPrefab.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("HDRP/Unlit"))
            {
                name = "PortalVisualizerMaterial",
                color = Color.white,
            };
            renderer.shadowCastingMode = ShadowCastingMode.Off;

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

        private void SpawnAllTileBoundsVisualizers()
        {
            Destroy(_tileBoundsVisualizersRoot);
            if (!Plugin.Configuration.VisualizeTileBounds.Value)
                return;
            _tileBoundsVisualizersRoot = new GameObject("TileBoundsVisualizers");

            var tileBoundsPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileBoundsPrefab.name = "TileBounds";

            DestroyImmediate(tileBoundsPrefab.GetComponent<Collider>());

            var renderer = tileBoundsPrefab.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("HDRP/Unlit"))
            {
                name = "TileBoundsVisualizerMaterial",
                color = Color.yellow,
            };
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var mesh = tileBoundsPrefab.GetComponent<MeshFilter>().sharedMesh;
            mesh.triangles = [.. mesh.triangles, .. mesh.triangles.Reverse()];

            var insetVector = new Vector3(TileBoundsInset, TileBoundsInset, TileBoundsInset);

            for (int i = 0; i < DungeonCullingInfo.AllTileContents.Length; i++)
            {
                var tile = DungeonCullingInfo.AllTileContents[i];
                var tileBoundsVisualizer = Instantiate(tileBoundsPrefab, _tileBoundsVisualizersRoot.transform);
                tileBoundsVisualizer.transform.position = tile.bounds.center;
                tileBoundsVisualizer.transform.localScale = tile.bounds.size - insetVector;
                tileBoundsVisualizer.GetComponent<Renderer>().material.color = ColorRotation[i % ColorRotation.Length];
            }

            Destroy(tileBoundsPrefab);
        }

        private void OnDisable()
        {
            Destroy(_portalVisualizersRoot);
            Destroy(_tileBoundsVisualizersRoot);
        }
    }
}
