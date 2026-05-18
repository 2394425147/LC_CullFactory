using System.Collections.Generic;
using CullFactory.Behaviours.CullingMethods;
using CullFactory.Data;
using CullFactory.Services;
using Unity.Collections;
using UnityEngine;

namespace CullFactory.Behaviours.Visualization;

internal sealed class FrustumVisualizer : MonoBehaviour
{
    private static Material[] _materials;

    private readonly List<Mesh> _meshPool = [];
    private readonly List<Camera> _camerasToVisualize = [];

    private readonly List<Plane> _planeBuffer = [];
    private int _meshesUsedThisFrame;

    public static void Initialize()
    {
        if (!CullingMethod.GetContainer(out var container))
            return;

        var existing = container.GetComponent<FrustumVisualizer>();
        if (existing != null)
            DestroyImmediate(existing);

        if (!Config.VisualizeFrustums.Value)
            return;

        container.AddComponent<FrustumVisualizer>();
    }

    private void LateUpdate()
    {
        _meshesUsedThisFrame = 0;

        if (!isActiveAndEnabled || !Config.VisualizeFrustums.Value)
            return;

        CollectCamerasToVisualize(_camerasToVisualize);
        if (_camerasToVisualize.Count == 0)
            return;

        foreach (var camera in _camerasToVisualize)
            DrawFrustumsForCamera(camera);
    }

    private void DrawFrustumsForCamera(Camera camera)
    {
        if (camera == null)
            return;
        var originTile = camera.transform.position.GetTileContents();
        if (originTile == null)
            return;

        var materials = GetMaterials();
        VisibilityTesting.CallForEachLineOfSight(camera, originTile, (tiles, frustums, stackIndex) =>
        {
            CopyCumulativePlanes(frustums, stackIndex);
            var mesh = AcquireMesh();
            if (!FrustumMeshBuilder.TryBuildWireframe(_planeBuffer, tiles[stackIndex].bounds, mesh))
                return;
            var material = materials[_meshesUsedThisFrame % materials.Length];
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, gameObject.layer, camera: null, submeshIndex: 0, properties: null, castShadows: false, receiveShadows: false, useLightProbes: false);
            _meshesUsedThisFrame++;
        });
    }

    private void CollectCamerasToVisualize(List<Camera> destination)
    {
        destination.Clear();

        var culler = CullingMethod.Instance;
        if (culler != null)
        {
            var culled = culler.CamerasCulledLastFrame;
            for (var i = 0; i < culled.Count; i++)
            {
                var cam = culled[i];
                if (cam != null)
                    destination.Add(cam);
            }
        }

        var playerCamera = StartOfRound.Instance.localPlayerController.gameplayCamera;
        if (playerCamera != null && !destination.Contains(playerCamera))
            destination.Add(playerCamera);
    }

    private Mesh AcquireMesh()
    {
        if (_meshesUsedThisFrame < _meshPool.Count)
        {
            var existing = _meshPool[_meshesUsedThisFrame];
            existing.Clear();
            return existing;
        }

        var mesh = new Mesh
        {
            name = $"FrustumVisualizerMesh_{_meshesUsedThisFrame}",
            hideFlags = HideFlags.HideAndDontSave,
        };
        mesh.MarkDynamic();
        _meshPool.Add(mesh);
        return mesh;
    }

    private static Material[] GetMaterials()
    {
        if (_materials != null)
            return _materials;

        var rotation = CullingVisualizer.ColorRotation;
        _materials = new Material[rotation.Length];
        for (var i = 0; i < rotation.Length; i++)
        {
            _materials[i] = new Material(Shader.Find("HDRP/Unlit"))
            {
                name = $"FrustumVisualizerMaterial_{i}",
                color = rotation[i],
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
        return _materials;
    }

    private void CopyCumulativePlanes(NativeSlice<Plane>[] frustums, int stackIndex)
    {
        _planeBuffer.Clear();
        for (var i = 0; i <= stackIndex; i++)
        {
            var slice = frustums[i];
            for (var j = 0; j < slice.Length; j++)
                _planeBuffer.Add(slice[j]);
        }
    }

    private void OnDisable()
    {
        for (var i = 0; i < _meshPool.Count; i++)
            Destroy(_meshPool[i]);
        _meshPool.Clear();

        if (_materials != null)
        {
            foreach (var material in _materials)
                Destroy(material);
            _materials = null;
        }
    }
}
