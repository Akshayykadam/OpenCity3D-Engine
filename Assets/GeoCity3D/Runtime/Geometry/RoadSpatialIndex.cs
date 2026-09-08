using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// High-performance 2D spatial hash grid for road networks.
    /// Provides exact distance-to-segment queries to guarantee that no trees, rocks,
    /// props, or grass can ever spawn on road asphalt or sidewalks.
    /// </summary>
    public static class RoadSpatialIndex
    {
        public struct RoadSegment
        {
            public Vector2 A;
            public Vector2 B;
            public Vector2 Dir; // Normalized direction from A to B
            public float Length;
            public float HalfWidth; // Road half-width + sidewalk
            public float MinY;
            public float MaxY;
        }

        public struct RoadJunction
        {
            public Vector2 Center;
            public float Radius;
            public float Y;
        }

        private const float CELL_SIZE = 40f;
        private static readonly List<RoadSegment> _segments = new List<RoadSegment>(4096);
        private static readonly List<RoadJunction> _junctions = new List<RoadJunction>(512);
        private static readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>(2048);
        private static readonly Dictionary<long, List<int>> _junctionGrid = new Dictionary<long, List<int>>(512);

        /// <summary>
        /// Clears all indexed road data. Call before generating roads.
        /// </summary>
        public static void Clear()
        {
            _segments.Clear();
            _junctions.Clear();
            _grid.Clear();
            _junctionGrid.Clear();
        }

        public static int SegmentCount => _segments.Count;
        public static int JunctionCount => _junctions.Count;

        /// <summary>
        /// Registers a road polyline path into the spatial index.
        /// </summary>
        public static void AddRoadPath(List<Vector3> path, float roadWidth, float sidewalkWidth = 2.0f)
        {
            if (path == null || path.Count < 2) return;

            float totalHalfWidth = (roadWidth * 0.5f) + sidewalkWidth;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 a = new Vector2(path[i].x, path[i].z);
                Vector2 b = new Vector2(path[i + 1].x, path[i + 1].z);
                Vector2 diff = b - a;
                float len = diff.magnitude;
                if (len < 0.001f) continue;

                RoadSegment seg = new RoadSegment
                {
                    A = a,
                    B = b,
                    Dir = diff / len,
                    Length = len,
                    HalfWidth = totalHalfWidth,
                    MinY = Mathf.Min(path[i].y, path[i + 1].y),
                    MaxY = Mathf.Max(path[i].y, path[i + 1].y)
                };

                int segIndex = _segments.Count;
                _segments.Add(seg);

                // Insert into grid cells covered by the segment AABB (expanded by totalHalfWidth)
                float minX = Mathf.Min(a.x, b.x) - totalHalfWidth;
                float maxX = Mathf.Max(a.x, b.x) + totalHalfWidth;
                float minZ = Mathf.Min(a.y, b.y) - totalHalfWidth;
                float maxZ = Mathf.Max(a.y, b.y) + totalHalfWidth;

                int cMinX = Mathf.FloorToInt(minX / CELL_SIZE);
                int cMaxX = Mathf.FloorToInt(maxX / CELL_SIZE);
                int cMinZ = Mathf.FloorToInt(minZ / CELL_SIZE);
                int cMaxZ = Mathf.FloorToInt(maxZ / CELL_SIZE);

                for (int cx = cMinX; cx <= cMaxX; cx++)
                {
                    for (int cz = cMinZ; cz <= cMaxZ; cz++)
                    {
                        long key = ((long)cx << 32) | (uint)cz;
                        if (!_grid.TryGetValue(key, out var list))
                        {
                            list = new List<int>(8);
                            _grid[key] = list;
                        }
                        list.Add(segIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Registers an intersection area into the spatial index.
        /// </summary>
        public static void AddIntersection(Vector3 center, float radius)
        {
            Vector2 c = new Vector2(center.x, center.z);
            RoadJunction junc = new RoadJunction { Center = c, Radius = radius, Y = center.y };

            int juncIndex = _junctions.Count;
            _junctions.Add(junc);

            int cMinX = Mathf.FloorToInt((c.x - radius) / CELL_SIZE);
            int cMaxX = Mathf.FloorToInt((c.x + radius) / CELL_SIZE);
            int cMinZ = Mathf.FloorToInt((c.y - radius) / CELL_SIZE);
            int cMaxZ = Mathf.FloorToInt((c.y + radius) / CELL_SIZE);

            for (int cx = cMinX; cx <= cMaxX; cx++)
            {
                for (int cz = cMinZ; cz <= cMaxZ; cz++)
                {
                    long key = ((long)cx << 32) | (uint)cz;
                    if (!_junctionGrid.TryGetValue(key, out var list))
                    {
                        list = new List<int>(4);
                        _junctionGrid[key] = list;
                    }
                    list.Add(juncIndex);
                }
            }
        }

        /// <summary>
        /// Checks if another road (ground or lower flyover) passes beneath a given point.
        /// Used by bridge support pillar placement to avoid putting pillars on lower roadways.
        /// </summary>
        public static bool IsRoadUnderneath(Vector3 pos, float currentDeckY, float clearanceBuffer = 1.5f)
        {
            if (_segments.Count == 0 && _junctions.Count == 0) return false;

            Vector2 pos2D = new Vector2(pos.x, pos.z);
            int cx = Mathf.FloorToInt(pos2D.x / CELL_SIZE);
            int cz = Mathf.FloorToInt(pos2D.y / CELL_SIZE);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    long key = ((long)(cx + dx) << 32) | (uint)(cz + dz);

                    if (_grid.TryGetValue(key, out var segIndices))
                    {
                        for (int i = 0; i < segIndices.Count; i++)
                        {
                            var seg = _segments[segIndices[i]];
                            if (seg.MaxY < currentDeckY - 1.5f)
                            {
                                float maxDist = seg.HalfWidth + clearanceBuffer;
                                float distSq = DistanceSqToSegment(pos2D, seg.A, seg.B, seg.Dir, seg.Length);
                                if (distSq < maxDist * maxDist) return true;
                            }
                        }
                    }

                    if (_junctionGrid.TryGetValue(key, out var juncIndices))
                    {
                        for (int i = 0; i < juncIndices.Count; i++)
                        {
                            var junc = _junctions[juncIndices[i]];
                            if (junc.Y < currentDeckY - 1.5f)
                            {
                                float maxR = junc.Radius + clearanceBuffer;
                                if ((pos2D - junc.Center).sqrMagnitude < maxR * maxR)
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tests if a 3D position is on or within clearanceBuffer distance of any road or sidewalk.
        /// </summary>
        public static bool IsPointOnRoad(Vector3 worldPos, float clearanceBuffer = 0.5f)
        {
            return IsPointOnRoad(new Vector2(worldPos.x, worldPos.z), clearanceBuffer);
        }

        /// <summary>
        /// Tests if a 2D position (XZ plane) is on or within clearanceBuffer distance of any road or sidewalk.
        /// </summary>
        public static bool IsPointOnRoad(Vector2 pos2D, float clearanceBuffer = 0.5f)
        {
            if (_segments.Count == 0 && _junctions.Count == 0) return false;

            int cx = Mathf.FloorToInt(pos2D.x / CELL_SIZE);
            int cz = Mathf.FloorToInt(pos2D.y / CELL_SIZE);

            // Check the 3x3 neighboring cells around the point to guarantee no boundary misses
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    long key = ((long)(cx + dx) << 32) | (uint)(cz + dz);

                    // 1. Check road segments in this cell
                    if (_grid.TryGetValue(key, out var segIndices))
                    {
                        for (int i = 0; i < segIndices.Count; i++)
                        {
                            var seg = _segments[segIndices[i]];
                            float maxDist = seg.HalfWidth + clearanceBuffer;
                            float maxDistSq = maxDist * maxDist;

                            float distSq = DistanceSqToSegment(pos2D, seg.A, seg.B, seg.Dir, seg.Length);
                            if (distSq < maxDistSq) return true;
                        }
                    }

                    // 2. Check junctions in this cell
                    if (_junctionGrid.TryGetValue(key, out var juncIndices))
                    {
                        for (int i = 0; i < juncIndices.Count; i++)
                        {
                            var junc = _junctions[juncIndices[i]];
                            float maxR = junc.Radius + clearanceBuffer;
                            if ((pos2D - junc.Center).sqrMagnitude < maxR * maxR)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Computes exact squared distance from a 2D point to a line segment.
        /// </summary>
        private static float DistanceSqToSegment(Vector2 p, Vector2 a, Vector2 b, Vector2 dir, float len)
        {
            Vector2 ap = p - a;
            float t = Vector2.Dot(ap, dir);
            if (t <= 0f) return ap.sqrMagnitude;
            if (t >= len) return (p - b).sqrMagnitude;
            Vector2 closest = a + dir * t;
            return (p - closest).sqrMagnitude;
        }
    }
}
