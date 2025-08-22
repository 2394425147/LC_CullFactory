using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace CullFactoryBurst
{

    [BurstCompile]
    public static class Geometry
    {
        private static bool WarnedCallWasNotBurstCompiled = false;

        [BurstDiscard]
        public static void WarnIfNotBurstCompiled()
        {
            if (!WarnedCallWasNotBurstCompiled)
            {
                Debug.LogWarning("Methods intended to be Burst-compiled in CullFactoryBurstPlugin are not using Burst.");
                WarnedCallWasNotBurstCompiled = true;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        public static unsafe bool TestPlanesAABB(Plane* planes, int planeCount, in Bounds bounds)
        {
            WarnIfNotBurstCompiled();

            for (int i = 0; i < planeCount; i++)
            {
                var plane = planes[i];

                var centerDistance = math.dot(plane.normal, bounds.center);
                var extentsDistance = math.dot(math.abs(plane.normal), bounds.extents);
                var result = centerDistance + extentsDistance + plane.distance;

                if (result <= 0)
                    return false;
            }

            return true;
        }

        public static unsafe bool TestPlanesAABB(in Plane[] planes, in Bounds bounds)
        {
            if (planes.Length == 0)
                return true;
            return TestPlanesAABB((Plane*)UnsafeUtility.AddressOf(ref planes[0]), planes.Length, bounds);
        }

        public static unsafe bool TestPlanesAABB(in Span<Plane> planes, in Bounds bounds)
        {
            if (planes.Length == 0)
                return true;
            return TestPlanesAABB((Plane*)UnsafeUtility.AddressOf(ref planes[0]), planes.Length, bounds);
        }

        #region Intersection of box and cone
        // Based on https://www.geometrictools.com/Documentation/IntersectionBoxCone.pdf

        private struct Cone
        {
            internal Vector3 origin;
            internal Vector3 direction;
            internal float length;
            internal float cosAngle;

            internal Cone(Vector3 origin, Vector3 direction, float length, float angle)
            {
                this.origin = origin;
                this.direction = direction;
                this.length = length;
                cosAngle = math.cos(math.radians(angle / 2));
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static void ComputeBoundsHeightInterval(in Bounds bounds, in Cone cone, out float minHeight, out float maxHeight)
        {
            float3 center = bounds.center;
            float3 extents = bounds.extents;

            float3 origin = cone.origin;
            float3 direction = cone.direction;

            var centerDistance = math.dot(direction, center - origin);
            var radius = bounds.extents.x * math.abs(direction.x) + bounds.extents.y * math.abs(direction.y) + extents.z * math.abs(direction.z);
            minHeight = centerDistance - radius;
            maxHeight = centerDistance + radius;
        }

        private struct EdgeIndices
        {
            internal int a;
            internal int b;

            internal EdgeIndices(int a, int b)
            {
                this.a = a;
                this.b = b;
            }
        }

        private static readonly EdgeIndices[] edges = new EdgeIndices[]
        {
            new(0, 1),
            new(1, 3),
            new(2, 3),
            new(0, 2),
            new(4, 5),
            new(5, 7),
            new(6, 7),
            new(4, 6),
            new(0, 4),
            new(1, 5),
            new(3, 7),
            new(2, 6)
        };

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static unsafe void AppendEdge(EdgeIndices* edges, ref int edgeCount, in EdgeIndices edge)
        {
            edges[edgeCount++] = edge;
        }

        const int CornerVertexCount = 8;
        const int MaxVertexCount = 32;

        const int ClipVerticesMinBaseIndex = 8;
        const int ClipVerticesMaxBaseIndex = 20;

        const int BoxEdgeCount = 12;
        const int MaxEdgeCandidateCount = (MaxVertexCount - 1) * (MaxVertexCount / 2);

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static unsafe void ComputeCandidatesOnBoundsEdges(in Cone cone, float3* vertices, float* projectionsMin, float* projectionsMax, EdgeIndices* candidates, ref int candidateCount)
        {
            for (var i = 0; i < CornerVertexCount; i++)
            {
                var h = math.dot(cone.direction, vertices[i]);
                projectionsMin[i] = -h;
                projectionsMax[i] = h - cone.length;
            }

            var vertexAIndex = ClipVerticesMinBaseIndex;
            var vertexBIndex = ClipVerticesMaxBaseIndex;

            for (var i = 0; i < BoxEdgeCount; i++)
            {
                var edge = edges[i];

                var projectionAMin = projectionsMin[edge.a];
                var projectionBMin = projectionsMin[edge.b];
                bool clipMin = (projectionAMin < 0 && projectionBMin > 0) || (projectionAMin > 0 && projectionBMin < 0);
                if (clipMin)
                    vertices[vertexAIndex] = (projectionBMin * vertices[edge.a] - projectionAMin * vertices[edge.b]) / (projectionBMin - projectionAMin);

                var projectionAMax = projectionsMax[edge.a];
                var projectionBMax = projectionsMax[edge.b];
                bool clipMax = (projectionAMax < 0 && projectionBMax > 0) || (projectionAMax > 0 && projectionBMax < 0);
                if (clipMax)
                    vertices[vertexBIndex] = (projectionBMax * vertices[edge.a] - projectionAMax * vertices[edge.b]) / (projectionBMax - projectionAMax);

                if (clipMin)
                {
                    if (clipMax)
                        AppendEdge(candidates, ref candidateCount, new EdgeIndices(vertexAIndex, vertexBIndex));
                    else if (projectionAMin < 0)
                        AppendEdge(candidates, ref candidateCount, new EdgeIndices(edge.a, vertexAIndex));
                    else
                        AppendEdge(candidates, ref candidateCount, new EdgeIndices(edge.b, vertexAIndex));
                }
                else if (clipMax)
                {
                    if (projectionAMax < 0)
                        AppendEdge(candidates, ref candidateCount, new EdgeIndices(edge.a, vertexBIndex));
                    else
                        AppendEdge(candidates, ref candidateCount, new EdgeIndices(edge.b, vertexBIndex));
                }
                else if (projectionAMin <= 0 && projectionBMin <= 0 && projectionAMax <= 0 && projectionBMax <= 0)
                {
                    AppendEdge(candidates, ref candidateCount, edge);
                }

                vertexAIndex++;
                vertexBIndex++;
            }
        }

        private struct FaceIndexSet
        {
            internal int a;
            internal int b;
            internal int c;
            internal int d;

            internal FaceIndexSet(int a, int b, int c, int d)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                this.d = d;
            }
        }

        private struct FaceIndices
        {
            internal FaceIndexSet vertices;
            internal FaceIndexSet edges;

            internal FaceIndices(FaceIndexSet vertices, FaceIndexSet edges)
            {
                this.vertices = vertices;
                this.edges = edges;
            }
        }

        private static readonly FaceIndices[] faces = new FaceIndices[]
        {
            new(new(0, 4, 6, 2), new(8, 7, 11, 3)),
            new(new(1, 3, 7, 5), new(1, 10, 5, 9)),
            new(new(0, 1, 5, 4), new(0, 9, 4, 8)),
            new(new(2, 6, 7, 3), new(11, 6, 10, 2)),
            new(new(0, 2, 3, 1), new(3, 2, 1, 0)),
            new(new(4, 5, 7, 6), new(4, 5, 6, 7)),
        };

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static unsafe void ApplyFaceConfiguration(in FaceIndices face, float* projections, int baseIndex, EdgeIndices* candidates, ref int candidateCount)
        {
            var projectionA = projections[face.vertices.a];
            var projectionB = projections[face.vertices.b];
            var projectionC = projections[face.vertices.c];
            var projectionD = projections[face.vertices.d];
            var configA = projectionA < 0 ? 0 : (projectionA > 0 ? 2 : 1);
            var configB = projectionB < 0 ? 0 : (projectionB > 0 ? 2 : 1);
            var configC = projectionC < 0 ? 0 : (projectionC > 0 ? 2 : 1);
            var configD = projectionD < 0 ? 0 : (projectionD > 0 ? 2 : 1);
            var config = configD + 3 * (configC + 3 * (configB + 3 * configA));

            switch (config)
            {
                case 2:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, baseIndex + face.edges.d));
                    break;
                case 5:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, baseIndex + face.edges.d));
                    break;
                case 6:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, baseIndex + face.edges.c));
                    break;
                case 7:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.d));
                    break;
                case 8:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, baseIndex + face.edges.d));
                    break;
                case 11:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.d));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.d));
                    break;
                case 14:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, face.vertices.d));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.d));
                    break;
                case 15:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.b));
                    break;
                case 16:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.d));
                    break;
                case 17:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.b));
                    break;
                case 18:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, baseIndex + face.edges.b));
                    break;
                case 19:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.b));
                    break;
                case 20:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.d));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.d));
                    break;
                case 21:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, face.vertices.c));
                    break;
                case 22:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.c));
                    break;
                case 23:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, face.vertices.d));
                    break;
                case 24:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, baseIndex + face.edges.c));
                    break;
                case 25:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, face.vertices.d));
                    break;
                case 26:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, baseIndex + face.edges.d));
                    break;
                case 29:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.a));
                    break;
                case 32:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.c));
                    break;
                case 33:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.c));
                    break;
                case 34:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, face.vertices.d));
                    break;
                case 35:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.b));
                    break;
                case 38:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.d));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, baseIndex + face.edges.c));
                    break;
                case 41:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.d));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, face.vertices.c));
                    break;
                case 42:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, baseIndex + face.edges.c));
                    break;
                case 43:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, face.vertices.d));
                    break;
                case 45:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.b));
                    break;
                case 46:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, baseIndex + face.edges.b));
                    break;
                case 47:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, baseIndex + face.edges.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.d));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, face.vertices.a));
                    break;
                case 48:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.c));
                    break;
                case 49:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.c));
                    break;
                case 51:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.c));
                    break;
                case 54:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, baseIndex + face.edges.a));
                    break;
                case 55:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, baseIndex + face.edges.a));
                    break;
                case 56:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, baseIndex + face.edges.a));
                    break;
                case 57:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.a));
                    break;
                case 58:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, face.vertices.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.a));
                    break;
                case 59:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, baseIndex + face.edges.a));
                    break;
                case 60:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, baseIndex + face.edges.c));
                    break;
                case 61:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, face.vertices.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, baseIndex + face.edges.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, face.vertices.d));
                    break;
                case 62:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.a, baseIndex + face.edges.b));
                    break;
                case 63:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.b));
                    break;
                case 64:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, face.vertices.b));
                    break;
                case 65:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.b));
                    break;
                case 66:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.b));
                    break;
                case 69:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, face.vertices.a));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.a, face.vertices.b));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.b, face.vertices.c));
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.c, baseIndex + face.edges.c));
                    break;
                case 72:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, baseIndex + face.edges.b));
                    break;
                case 73:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(face.vertices.d, baseIndex + face.edges.b));
                    break;
                case 74:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, baseIndex + face.edges.b));
                    break;
                case 75:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.c, face.vertices.c));
                    break;
                case 78:
                    AppendEdge(candidates, ref candidateCount, new EdgeIndices(baseIndex + face.edges.d, baseIndex + face.edges.c));
                    break;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static unsafe void ComputeCandidatesOnBoundsFaces(float3* vertices, float* projectionsMin, float* projectionsMax, EdgeIndices* candidates, ref int candidateCount)
        {
            for (var i = 0; i < 6; i++)
            {
                var face = faces[i];
                ApplyFaceConfiguration(face, projectionsMin, ClipVerticesMinBaseIndex, candidates, ref candidateCount);
                ApplyFaceConfiguration(face, projectionsMax, ClipVerticesMaxBaseIndex, candidates, ref candidateCount);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static unsafe bool EdgeHasPointInsideCone(in float3 pointA, in float3 pointB, in Cone cone)
        {
            var g = math.dot(cone.direction, pointA) - cone.cosAngle * math.length(pointA);
            if (g > 0)
                return true;
            g = math.dot(cone.direction, pointB) - cone.cosAngle * math.length(pointB);
            if (g > 0)
                return true;

            var edgeDelta = pointB - pointA;
            var pointACrossCone = math.cross(pointA, cone.direction);
            var pointACrossDelta = math.cross(pointA, edgeDelta);
            var dPhiA = math.dot(pointACrossDelta, pointACrossCone);
            if (dPhiA > 0)
            {
                var pointBCrossCone = math.cross(pointB, cone.direction);
                var dPhiB = math.dot(pointACrossDelta, pointBCrossCone);
                if (dPhiB < 0)
                {
                    var t = dPhiA / (dPhiA - dPhiB);
                    var pMax = pointA + t * edgeDelta;
                    g = math.dot(cone.direction, pMax) - cone.cosAngle * math.length(pMax);
                    if (g > 0)
                        return true;
                }
            }

            return false;
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static unsafe bool CandidatesHavePointInsideCone(in Cone cone, float3* vertices, EdgeIndices* candidates, int candidateCount)
        {
            for (var i = 0; i < candidateCount; i++)
            {
                var edge = candidates[i];
                var vertexA = vertices[edge.a];
                var vertexB = vertices[edge.b];
                
                if (EdgeHasPointInsideCone(vertexA, vertexB, cone))
                    return true;
            }
            return false;
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private static bool ConeIntersectsBounds(in Cone cone, in Bounds bounds)
        {
            WarnIfNotBurstCompiled();

            ComputeBoundsHeightInterval(bounds, cone, out var minHeight, out var maxHeight);
            if (maxHeight <= 0 || minHeight >= cone.length)
                return false;

            if (bounds.IntersectRay(new Ray(cone.origin, cone.direction), out _))
                return true;

            unsafe
            {
                float3 min = bounds.min;
                float3 max = bounds.max;
                float3 origin = cone.origin;

                var vertices = stackalloc float3[MaxVertexCount];
                vertices[0] = new float3(min.x, min.y, min.z) - origin;
                vertices[1] = new float3(max.x, min.y, min.z) - origin;
                vertices[2] = new float3(min.x, max.y, min.z) - origin;
                vertices[3] = new float3(max.x, max.y, min.z) - origin;
                vertices[4] = new float3(min.x, min.y, max.z) - origin;
                vertices[5] = new float3(max.x, min.y, max.z) - origin;
                vertices[6] = new float3(min.x, max.y, max.z) - origin;
                vertices[7] = new float3(max.x, max.y, max.z) - origin;

                var candidates = stackalloc EdgeIndices[MaxVertexCount * MaxVertexCount];
                var candidateCount = 0;
                if (minHeight >= 0 && maxHeight <= cone.length)
                {
                    for (var i = 0; i < BoxEdgeCount; i++)
                    {
                        candidates[i] = edges[i];
                        candidateCount++;
                    }
                    return CandidatesHavePointInsideCone(cone, vertices, candidates, candidateCount);
                }

                var projectionsMin = stackalloc float[CornerVertexCount];
                var projectionsMax = stackalloc float[CornerVertexCount];
                ComputeCandidatesOnBoundsEdges(cone, vertices, projectionsMin, projectionsMax, candidates, ref candidateCount);

                ComputeCandidatesOnBoundsFaces(vertices, projectionsMin, projectionsMax, candidates, ref candidateCount);

                return CandidatesHavePointInsideCone(cone, vertices, candidates, candidateCount);
            }
        }

        public static bool SpotLightInfluencesBounds(in Light light, in Bounds bounds)
        {
            var lightTransform = light.transform;
            var cone = new Cone(lightTransform.position, lightTransform.forward, light.range, light.spotAngle);
            return ConeIntersectsBounds(cone, bounds);
        }

        #endregion
    }

}