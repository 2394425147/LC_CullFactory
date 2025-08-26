using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Services;

public static class BoundsUtility
{
    public static Plane[] GetPlanes(this in Bounds bounds)
    {
        return [
            new Plane(new Vector3(1, 0, 0), -bounds.min.x),
            new Plane(new Vector3(0, 1, 0), -bounds.min.y),
            new Plane(new Vector3(0, 0, 1), -bounds.min.z),
            new Plane(new Vector3(-1, 0, 0), bounds.max.x),
            new Plane(new Vector3(0, -1, 0), bounds.max.y),
            new Plane(new Vector3(0, 0, -1), bounds.max.z),
        ];
    }

    public static Vector3[] GetVertices(Vector3 min, Vector3 max)
    {
        return [
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        ];
    }

    public static Vector3[] GetVertices(this Bounds bounds)
    {
        return GetVertices(bounds.min, bounds.max);
    }

    private struct AABBFrustumEdge(int faceA, int faceB, int vertA, int vertB)
    {
        internal int FaceA = faceA;
        internal int FaceB = faceB;
        internal int VertA = vertA;
        internal int VertB = vertB;
    }
    private struct AABBFrustumData
    {
        internal Vector3[] Normals;
        internal Vector3[] Vertices;
        internal AABBFrustumEdge[] Edges;
    }

    private static AABBFrustumData GetFrustumData()
    {
        var normals = new Vector3[] {
            Vector3.right,
            Vector3.up,
            Vector3.forward,
            Vector3.left,
            Vector3.down,
            Vector3.back,
        };
        var verts = new List<Vector3>(8);
        var edges = new List<AABBFrustumEdge>(12);

        for (var i = 0; i < normals.Length; i++)
        {
            var normalA = normals[i];

            for (var j = 0; j < normals.Length; j++)
            {
                var normalB = normals[j];

                if (Vector3.Dot(normalA, normalB) < 0)
                    continue;

                var edgeDirection = Vector3.Cross(normalA, normalB);
                var vertA = normalA + normalB + edgeDirection;
                var vertB = normalA + normalB - edgeDirection;

                int AddVert(Vector3 vert)
                {
                    var vertIndex = verts.IndexOf(vert);
                    if (vertIndex != -1)
                        return vertIndex;
                    vertIndex = verts.Count;
                    verts.Add(vert);
                    return vertIndex;
                }

                var vertAIndex = AddVert(vertA);
                var vertBIndex = AddVert(vertB);
                edges.Add(new(i, j, vertAIndex, vertBIndex));
            }
        }

        return new AABBFrustumData()
        {
            Normals = normals,
            Vertices = [.. verts],
            Edges = [.. edges],
        };
    }

    private static readonly AABBFrustumData FrustumData = GetFrustumData();
    private static readonly List<Plane> TempPlanes = new(9);

    private static bool IsFacing(in Bounds bounds, in Vector3 normal, in Vector3 point)
    {
        var delta = bounds.center + Vector3.Scale(bounds.extents, normal) - point;
        return Vector3.Dot(normal, delta) < 0;
    }

    public static Plane[] GetFrustumFromPoint(this in Bounds bounds, Vector3 point)
    {
        if (bounds.Contains(point))
            return bounds.GetPlanes();

        TempPlanes.Clear();
        AddInsidePlanesFacingPointToTempPlanes(in bounds, in point);

        foreach (var edge in FrustumData.Edges)
        {
            var normalA = FrustumData.Normals[edge.FaceA];
            var normalB = FrustumData.Normals[edge.FaceB];
            if (!IsFacing(in bounds, in normalA, in point) || IsFacing(in bounds, in normalB, in point))
                continue;

            var vertA = bounds.center + Vector3.Scale(FrustumData.Vertices[edge.VertA], bounds.extents);
            var vertB = bounds.center + Vector3.Scale(FrustumData.Vertices[edge.VertB], bounds.extents);
            TempPlanes.Add(new(vertA, vertB, point));
        }

        return [.. TempPlanes];
    }

    public static void AddInsidePlanesFacingPointToTempPlanes(this in Bounds bounds, in Vector3 point)
    {
        foreach (var normal in FrustumData.Normals)
        {
            if (!IsFacing(in bounds, in normal, in point))
                TempPlanes.Add(new(-normal, bounds.center + Vector3.Scale(bounds.extents, normal)));
        }
    }

    public static Plane[] GetInsidePlanesFacingPoint(this in Bounds bounds, in Vector3 point)
    {
        TempPlanes.Clear();
        AddInsidePlanesFacingPointToTempPlanes(in bounds, in point);
        return [.. TempPlanes];
    }
}
