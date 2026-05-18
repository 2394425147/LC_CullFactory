using System;
using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Services;

internal static class FrustumMeshBuilder
{
    private const float PositionEpsilon = 1e-4f;

    public static bool TryBuildWireframe(List<Plane> planes, in Bounds clipBounds, Mesh destination)
    {
        destination.Clear();

        var faces = CreateBoxFaces(clipBounds);

        for (var i = 0; i < planes.Count; i++)
        {
            faces = ClipByPlane(faces, planes[i]);
            if (faces.Count == 0)
                return false;
        }

        return FillLineMesh(faces, destination);
    }

    private static List<List<Vector3>> CreateBoxFaces(in Bounds bounds)
    {
        var min = bounds.min;
        var max = bounds.max;
        var c000 = new Vector3(min.x, min.y, min.z);
        var c100 = new Vector3(max.x, min.y, min.z);
        var c010 = new Vector3(min.x, max.y, min.z);
        var c110 = new Vector3(max.x, max.y, min.z);
        var c001 = new Vector3(min.x, min.y, max.z);
        var c101 = new Vector3(max.x, min.y, max.z);
        var c011 = new Vector3(min.x, max.y, max.z);
        var c111 = new Vector3(max.x, max.y, max.z);

        // Each face stored CCW when viewed from outside the box.
        return
        [
            [c000, c001, c011, c010], // -x
            [c100, c110, c111, c101], // +x
            [c000, c100, c101, c001], // -y
            [c010, c011, c111, c110], // +y
            [c000, c010, c110, c100], // -z
            [c001, c101, c111, c011], // +z
        ];
    }

    private static List<List<Vector3>> ClipByPlane(List<List<Vector3>> faces, Plane plane)
    {
        var clippedFaces = new List<List<Vector3>>(faces.Count + 1);
        var cuttingPlaneVertices = new List<Vector3>();

        foreach (var face in faces)
        {
            ClipPolygon(face, plane, out var clippedFace, out var cuttingPlaneEdge);
            if (clippedFace.Count >= 3)
                clippedFaces.Add(clippedFace);
            if (cuttingPlaneEdge.HasValue)
            {
                cuttingPlaneVertices.Add(cuttingPlaneEdge.Value.entry);
                cuttingPlaneVertices.Add(cuttingPlaneEdge.Value.exit);
            }
        }

        var cuttingPlaneFace = StitchPlaneFace(cuttingPlaneVertices, plane);
        if (cuttingPlaneFace != null)
            clippedFaces.Add(cuttingPlaneFace);

        return clippedFaces;
    }

    private static void ClipPolygon(List<Vector3> polygon, Plane plane, out List<Vector3> clippedPolygon, out (Vector3 entry, Vector3 exit)? cuttingPlaneEdge)
    {
        cuttingPlaneEdge = null;

        clippedPolygon = new List<Vector3>(polygon.Count + 1);
        Vector3? entry = null;
        Vector3? exit = null;

        for (var i = 0; i < polygon.Count; i++)
        {
            var currentVertex = polygon[i];
            var nextVertex = polygon[(i + 1) % polygon.Count];
            var currentDistance = plane.GetDistanceToPoint(currentVertex);
            var nextDistance = plane.GetDistanceToPoint(nextVertex);

            if (currentDistance >= 0)
                clippedPolygon.Add(currentVertex);

            if ((currentDistance >= 0) != (nextDistance >= 0))
            {
                var crossingFraction = currentDistance / (currentDistance - nextDistance);
                var intersectionPoint = Vector3.Lerp(currentVertex, nextVertex, crossingFraction);
                clippedPolygon.Add(intersectionPoint);

                if (currentDistance >= 0)
                    exit = intersectionPoint;
                else
                    entry = intersectionPoint;
            }
        }

        if (!entry.HasValue || !exit.HasValue)
            return;
        cuttingPlaneEdge = (entry.Value, exit.Value);
    }

    private static List<Vector3> StitchPlaneFace(List<Vector3> edgeEndpoints, Plane plane)
    {
        var faceVertices = new List<Vector3>();
        foreach (var endpoint in edgeEndpoints)
        {
            var isDuplicate = false;
            for (var i = 0; i < faceVertices.Count; i++)
            {
                if ((faceVertices[i] - endpoint).sqrMagnitude < PositionEpsilon * PositionEpsilon)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
                faceVertices.Add(endpoint);
        }

        if (faceVertices.Count < 3)
            return null;

        var centroid = Vector3.zero;
        foreach (var vertex in faceVertices)
            centroid += vertex;
        centroid /= faceVertices.Count;

        // Sort the vertices by angle around the centroid to give the face a consistent winding.
        var planeNormal = plane.normal;
        var referenceAxis = Mathf.Abs(planeNormal.x) > 0.9f ? Vector3.up : Vector3.right;
        var tangent = Vector3.Normalize(referenceAxis - Vector3.Dot(referenceAxis, planeNormal) * planeNormal);
        var bitangent = Vector3.Cross(planeNormal, tangent);

        faceVertices.Sort((firstVertex, secondVertex) =>
        {
            var firstOffset = firstVertex - centroid;
            var secondOffset = secondVertex - centroid;
            var firstAngle = Mathf.Atan2(Vector3.Dot(firstOffset, bitangent), Vector3.Dot(firstOffset, tangent));
            var secondAngle = Mathf.Atan2(Vector3.Dot(secondOffset, bitangent), Vector3.Dot(secondOffset, tangent));
            return firstAngle.CompareTo(secondAngle);
        });

        return faceVertices;
    }

    private static bool FillLineMesh(List<List<Vector3>> faces, Mesh destination)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var emittedEdges = new HashSet<Edge>();
        var vertexIndicesByPosition = new Dictionary<Vector3, int>(new QuantizedVectorComparer());

        int GetOrAddVertexIndex(Vector3 vertex)
        {
            if (vertexIndicesByPosition.TryGetValue(vertex, out var existingIndex))
                return existingIndex;
            var newIndex = vertices.Count;
            vertices.Add(vertex);
            vertexIndicesByPosition[vertex] = newIndex;
            return newIndex;
        }

        foreach (var face in faces)
        {
            for (var i = 0; i < face.Count; i++)
            {
                var startIndex = GetOrAddVertexIndex(face[i]);
                var endIndex = GetOrAddVertexIndex(face[(i + 1) % face.Count]);
                if (startIndex == endIndex)
                    continue;
                if (!emittedEdges.Add(new Edge(startIndex, endIndex)))
                    continue;
                indices.Add(startIndex);
                indices.Add(endIndex);
            }
        }

        if (indices.Count == 0)
            return false;

        if (vertices.Count > ushort.MaxValue)
            destination.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        else
            destination.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        destination.SetVertices(vertices);
        destination.SetIndices(indices, MeshTopology.Lines, 0, calculateBounds: true);
        return true;
    }

    private struct Edge(int a, int b) : IEquatable<Edge>
    {
        public int a = Math.Min(a, b);
        public int b = Math.Max(a, b);

        public readonly bool Equals(Edge other)
        {
            return a == other.a && b == other.b;
        }

        public override readonly bool Equals(object other)
        {
            if (other is Edge edge)
                return Equals(edge);
            return false;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(a, b);
        }
    }

    private sealed class QuantizedVectorComparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < PositionEpsilon * PositionEpsilon;

        public int GetHashCode(Vector3 v)
        {
            const float reciprocal = 1f / PositionEpsilon;
            var x = Mathf.RoundToInt(v.x * reciprocal);
            var y = Mathf.RoundToInt(v.y * reciprocal);
            var z = Mathf.RoundToInt(v.z * reciprocal);
            return HashCode.Combine(x, y, z);
        }
    }
}
