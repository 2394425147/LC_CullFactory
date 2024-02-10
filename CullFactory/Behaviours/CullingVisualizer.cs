using DunGen;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours
{
    internal class CullingVisualizer : MonoBehaviour
    {
        private GameObject _portalVisualizersRoot;

        private void OnEnable()
        {
            RefreshVisualizers();
        }

        public void RefreshVisualizers()
        {
            SpawnAllPortalVisualizers();
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

            Destroy(_portalVisualizersRoot);
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

        private void OnDisable()
        {
            Destroy(_portalVisualizersRoot);
        }
    }
}
