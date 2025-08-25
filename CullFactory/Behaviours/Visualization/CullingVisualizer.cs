using System.Linq;
using CullFactory.Behaviours.CullingMethods;
using CullFactory.Data;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.Visualization
{
    internal class CullingVisualizer : MonoBehaviour
    {
        private const float TileBoundsInset = 0.00025f;

        private static readonly Color[] ColorRotation = [Color.red, Color.yellow, Color.green, Color.blue, Color.cyan, Color.grey];

        private GameObject _portalVisualizersRoot;
        private GameObject _tileBoundsVisualizersRoot;

        public static void Initialize()
        {
            if (!CullingMethod.GetContainer(out var container))
                return;

            DestroyImmediate(container.GetComponent<CullingVisualizer>());
            container.AddComponent<CullingVisualizer>();
        }

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

            for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
            {
                ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];
                if (!dungeonData.DungeonRef.TryGetTarget(out var dungeon))
                    continue;

                foreach (var doorwayConnection in dungeon.Connections)
                {
                    SpawnPortalVisualizer(portalPrefab, doorwayConnection.A);
                    SpawnPortalVisualizer(portalPrefab, doorwayConnection.B);
                }
            }

            Destroy(portalPrefab.gameObject);
        }

        private void SpawnTileBoundsVisualizers()
        {
            Destroy(_tileBoundsVisualizersRoot);
            if (!Config.VisualizeTileBounds.Value)
                return;
            _tileBoundsVisualizersRoot = new GameObject("TileBoundsVisualizers");

            var tileBoundsPrefab = CreateTileBoundsVisualizer();

            var insetVector = new Vector3(TileBoundsInset, TileBoundsInset, TileBoundsInset);

            var colorIndex = 0;
            for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
            {
                ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];

                foreach (var tile in dungeonData.AllTileContents)
                {
                    var tileBoundsVisualizer = Instantiate(tileBoundsPrefab, _tileBoundsVisualizersRoot.transform);
                    tileBoundsVisualizer.transform.position = tile.bounds.center;
                    tileBoundsVisualizer.transform.localScale = tile.bounds.size - insetVector;
                    tileBoundsVisualizer.material.color = ColorRotation[colorIndex % ColorRotation.Length];
                    colorIndex++;
                }
            }

            Destroy(tileBoundsPrefab.gameObject);
        }

        private static Transform CreatePortalVisualizer()
        {
            var portalPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portalPrefab.name = "PortalVisualizer";

            // Destroy is deferred, so destroy the collider immediately to avoid cloning it
            // when using this as a prefab.
            DestroyImmediate(portalPrefab.GetComponent<Collider>());

            var renderer = portalPrefab.GetComponent<Renderer>();
            renderer.material = new(Shader.Find("HDRP/Unlit"))
            {
                name = "PortalVisualizerMaterial",
                color = Color.white,
            };
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
            renderer.material = new(Shader.Find("HDRP/Unlit"))
            {
                name = "TileBoundsVisualizerMaterial",
                color = Color.yellow,
            };
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
