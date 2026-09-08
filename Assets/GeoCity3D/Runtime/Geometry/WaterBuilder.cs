using System.Collections.Generic;
using UnityEngine;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using GeoCity3D.Visuals;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Dedicated builder for volumetric water bodies: lakes, reservoirs, ponds, and rivers.
    /// Ensures water is rendered at a visible elevation (y = 0.04m) above the ground platform,
    /// with elegant shoreline banks, proper UV mapping, and no collision with road intersection networks.
    /// </summary>
    public static class WaterBuilder
    {
        public const float WATER_SURFACE_Y = 0.04f;
        public const float RIVER_SURFACE_Y = 0.035f;
        public const float SHORE_DEPTH = 0.04f;

        /// <summary>
        /// Builds a lake / water area from an OSM way.
        /// </summary>
        public static GameObject BuildLake(OsmWay way, OsmData data, Material waterMat,
            OriginShifter originShifter, string namePrefix = "Lake")
        {
            List<Vector3> polygon = new List<Vector3>();

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    Vector3 pos = originShifter.GetLocalPosition(node.Latitude, node.Longitude);
                    polygon.Add(pos);
                }
            }

            if (polygon.Count < 3) return null;

            if (Vector3.Distance(polygon[0], polygon[polygon.Count - 1]) < 0.1f)
                polygon.RemoveAt(polygon.Count - 1);

            if (polygon.Count < 3) return null;

            return BuildLake(polygon, waterMat, way.Id, $"{namePrefix}_{way.Id}");
        }

        /// <summary>
        /// Builds a lake / water area from an arbitrary 2D/3D polygon.
        /// </summary>
        public static GameObject BuildLake(List<Vector3> polygon, Material waterMat, long id, string name = "Lake")
        {
            if (polygon == null || polygon.Count < 3) return null;

            // 1. Clean near-duplicate points to ensure robust triangulation
            List<Vector3> clean = new List<Vector3>();
            for (int i = 0; i < polygon.Count; i++)
            {
                if (clean.Count == 0 || Vector3.Distance(new Vector3(polygon[i].x, 0, polygon[i].z),
                                                         new Vector3(clean[clean.Count - 1].x, 0, clean[clean.Count - 1].z)) > 0.15f)
                {
                    clean.Add(new Vector3(polygon[i].x, WATER_SURFACE_Y, polygon[i].z));
                }
            }
            if (clean.Count >= 3 && Vector3.Distance(new Vector3(clean[0].x, 0, clean[0].z),
                                                     new Vector3(clean[clean.Count - 1].x, 0, clean[clean.Count - 1].z)) <= 0.15f)
            {
                clean.RemoveAt(clean.Count - 1);
            }
            if (clean.Count < 3) return null;

            // Compute bounding box for metric UV mapping
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < clean.Count; i++)
            {
                if (clean[i].x < minX) minX = clean[i].x;
                if (clean[i].x > maxX) maxX = clean[i].x;
                if (clean[i].z < minZ) minZ = clean[i].z;
                if (clean[i].z > maxZ) maxZ = clean[i].z;
            }
            float sizeX = Mathf.Max(maxX - minX, 1.0f);
            float sizeZ = Mathf.Max(maxZ - minZ, 1.0f);

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            mesh.name = $"Mesh_{name}";
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // ── Top Water Surface ──
            int baseIdx = verts.Count;
            for (int i = 0; i < clean.Count; i++)
            {
                verts.Add(clean[i]);
                // World metric tiling UVs
                uvs.Add(new Vector2((clean[i].x - minX) / 20f, (clean[i].z - minZ) / 20f));
            }

            List<int> capTris = GeometryUtils.Triangulate(clean);
            if (capTris != null && capTris.Count >= 3)
            {
                for (int i = 0; i < capTris.Count; i++)
                    tris.Add(baseIdx + capTris[i]);
            }

            // ── Perimeter Shoreline Walls (from water surface down to ground) ──
            float groundY = WATER_SURFACE_Y - SHORE_DEPTH;
            for (int i = 0; i < clean.Count; i++)
            {
                int next = (i + 1) % clean.Count;
                Vector3 p1 = clean[i];
                Vector3 p2 = clean[next];

                int bi = verts.Count;
                verts.Add(new Vector3(p1.x, WATER_SURFACE_Y, p1.z));
                verts.Add(new Vector3(p2.x, WATER_SURFACE_Y, p2.z));
                verts.Add(new Vector3(p2.x, groundY, p2.z));
                verts.Add(new Vector3(p1.x, groundY, p1.z));

                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 0));

                tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                tris.Add(bi); tris.Add(bi + 3); tris.Add(bi + 2);
            }

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;

            return go;
        }

        /// <summary>
        /// Builds a linear river waterway from an OSM way.
        /// </summary>
        public static GameObject BuildRiver(OsmWay way, OsmData data, Material waterMat,
            OriginShifter originShifter, float riverWidth)
        {
            List<Vector3> path = new List<Vector3>();

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    Vector3 pos = originShifter.GetLocalPosition(node.Latitude, node.Longitude);
                    path.Add(pos);
                }
            }

            if (path.Count < 2) return null;

            return BuildRiver(path, riverWidth, waterMat, way.Id, $"River_{way.Id}");
        }

        /// <summary>
        /// Builds a linear river waterway mesh from a centerline path.
        /// Applies Catmull-Rom spline smoothing, creates water surface ribbon and side banks.
        /// </summary>
        public static GameObject BuildRiver(List<Vector3> rawPath, float width, Material waterMat, long id, string name = "River")
        {
            if (rawPath == null || rawPath.Count < 2) return null;

            // 1. Clean path
            List<Vector3> path = new List<Vector3> { rawPath[0] };
            for (int i = 1; i < rawPath.Count; i++)
            {
                if (Vector3.Distance(rawPath[i], path[path.Count - 1]) > 0.5f)
                    path.Add(rawPath[i]);
            }
            if (path.Count < 2) return null;

            // 2. Smooth path with Catmull-Rom splines
            if (path.Count >= 3)
            {
                path = GeometryUtils.SmoothPath(path, 4);
            }

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            mesh.name = $"Mesh_{name}";

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float halfW = width * 0.5f;
            float cumDist = 0f;
            float bankExtraW = Mathf.Clamp(width * 0.08f, 0.3f, 1.2f);
            float groundY = RIVER_SURFACE_Y - SHORE_DEPTH;

            // Precalculate segment lengths & cumulative distance
            List<float> distances = new List<float> { 0f };
            for (int i = 1; i < path.Count; i++)
            {
                cumDist += Vector3.Distance(path[i - 1], path[i]);
                distances.Add(cumDist);
            }

            // Generate vertices: 4 vertices per cross section:
            // 0: Outer left bank (y = groundY)
            // 1: Inner left water edge (y = RIVER_SURFACE_Y)
            // 2: Inner right water edge (y = RIVER_SURFACE_Y)
            // 3: Outer right bank (y = groundY)
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 forward = GetForward(path, i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 center = path[i];

                Vector3 waterL = center - right * halfW;
                waterL.y = RIVER_SURFACE_Y;

                Vector3 waterR = center + right * halfW;
                waterR.y = RIVER_SURFACE_Y;

                Vector3 bankL = center - right * (halfW + bankExtraW);
                bankL.y = groundY;

                Vector3 bankR = center + right * (halfW + bankExtraW);
                bankR.y = groundY;

                float v = distances[i] / 15f; // Tiling along river length

                verts.Add(bankL);
                uvs.Add(new Vector2(0f, v));

                verts.Add(waterL);
                uvs.Add(new Vector2(0.15f, v));

                verts.Add(waterR);
                uvs.Add(new Vector2(0.85f, v));

                verts.Add(bankR);
                uvs.Add(new Vector2(1.0f, v));
            }

            // Connect segments with triangles
            for (int i = 0; i < path.Count - 1; i++)
            {
                int row1 = i * 4;
                int row2 = (i + 1) * 4;

                // Left Bank strip: bankL1 -> waterL1 -> waterL2 -> bankL2
                AddQuad(tris, row1 + 0, row1 + 1, row2 + 1, row2 + 0);

                // Water Surface strip: waterL1 -> waterR1 -> waterR2 -> waterL2
                AddQuad(tris, row1 + 1, row1 + 2, row2 + 2, row2 + 1);

                // Right Bank strip: waterR1 -> bankR1 -> bankR2 -> waterR2
                AddQuad(tris, row1 + 2, row1 + 3, row2 + 3, row2 + 2);
            }

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;

            return go;
        }

        private static void AddQuad(List<int> tris, int tl, int tr, int br, int bl)
        {
            tris.Add(tl); tris.Add(tr); tris.Add(br);
            tris.Add(tl); tris.Add(br); tris.Add(bl);
        }

        private static Vector3 GetForward(List<Vector3> path, int i)
        {
            if (path.Count < 2) return Vector3.forward;
            if (i == 0) return (path[1] - path[0]).normalized;
            if (i == path.Count - 1) return (path[path.Count - 1] - path[path.Count - 2]).normalized;
            return ((path[i] - path[i - 1]).normalized + (path[i + 1] - path[i]).normalized).normalized;
        }

        /// <summary>
        /// Determines if a world-space point lies within any registered water area or linear waterway.
        /// An optional margin (in meters) expands the exclusion boundary so vegetation and props never touch water.
        /// </summary>
        public static bool IsPointInWater(Vector3 pos, List<WaterAreaInfo> waterAreas, List<WaterwayInfo> waterways, float margin = 0.5f)
        {
            if (waterAreas != null)
            {
                for (int i = 0; i < waterAreas.Count; i++)
                {
                    if (waterAreas[i] != null && waterAreas[i].Contains(pos, margin))
                        return true;
                }
            }

            if (waterways != null)
            {
                for (int i = 0; i < waterways.Count; i++)
                {
                    if (waterways[i] != null && waterways[i].Contains(pos, margin))
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Encapsulates a 2D water polygon area (lake, basin, riverbank) with fast bounding box filtering
    /// and exact point-in-polygon testing.
    /// </summary>
    public class WaterAreaInfo
    {
        public List<Vector3> Polygon { get; private set; }
        public float MinX { get; private set; }
        public float MaxX { get; private set; }
        public float MinZ { get; private set; }
        public float MaxZ { get; private set; }

        public WaterAreaInfo(List<Vector3> poly)
        {
            if (poly == null || poly.Count < 3)
            {
                Polygon = new List<Vector3>();
                MinX = MaxX = MinZ = MaxZ = 0f;
                return;
            }

            Polygon = new List<Vector3>();
            for (int i = 0; i < poly.Count; i++)
            {
                if (Polygon.Count == 0 || Vector3.Distance(new Vector3(poly[i].x, 0, poly[i].z),
                                                           new Vector3(Polygon[Polygon.Count - 1].x, 0, Polygon[Polygon.Count - 1].z)) > 0.1f)
                {
                    Polygon.Add(new Vector3(poly[i].x, 0, poly[i].z));
                }
            }
            if (Polygon.Count >= 3 && Vector3.Distance(new Vector3(Polygon[0].x, 0, Polygon[0].z),
                                                       new Vector3(Polygon[Polygon.Count - 1].x, 0, Polygon[Polygon.Count - 1].z)) <= 0.1f)
            {
                Polygon.RemoveAt(Polygon.Count - 1);
            }

            MinX = float.MaxValue; MaxX = float.MinValue;
            MinZ = float.MaxValue; MaxZ = float.MinValue;
            for (int i = 0; i < Polygon.Count; i++)
            {
                if (Polygon[i].x < MinX) MinX = Polygon[i].x;
                if (Polygon[i].x > MaxX) MaxX = Polygon[i].x;
                if (Polygon[i].z < MinZ) MinZ = Polygon[i].z;
                if (Polygon[i].z > MaxZ) MaxZ = Polygon[i].z;
            }
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            if (Polygon == null || Polygon.Count < 3) return false;
            if (p.x < MinX - margin || p.x > MaxX + margin ||
                p.z < MinZ - margin || p.z > MaxZ + margin)
                return false;

            if (GeometryUtils.PointInPolygon(p.x, p.z, Polygon))
                return true;

            if (margin > 0.001f)
            {
                float marginSqr = margin * margin;
                int n = Polygon.Count;
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    if (GeometryUtils.DistancePointToSegmentSqr2D(p, Polygon[i], Polygon[next]) <= marginSqr)
                        return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Encapsulates a linear waterway corridor (river, canal, stream) with width and fast segment distance checking.
    /// </summary>
    public class WaterwayInfo
    {
        public List<Vector3> Path { get; private set; }
        public float Width { get; private set; }
        public float HalfWidth { get; private set; }
        public float MinX { get; private set; }
        public float MaxX { get; private set; }
        public float MinZ { get; private set; }
        public float MaxZ { get; private set; }

        public WaterwayInfo(List<Vector3> path, float width)
        {
            Path = path ?? new List<Vector3>();
            Width = width;
            HalfWidth = width * 0.5f;

            MinX = float.MaxValue; MaxX = float.MinValue;
            MinZ = float.MaxValue; MaxZ = float.MinValue;
            for (int i = 0; i < Path.Count; i++)
            {
                if (Path[i].x < MinX) MinX = Path[i].x;
                if (Path[i].x > MaxX) MaxX = Path[i].x;
                if (Path[i].z < MinZ) MinZ = Path[i].z;
                if (Path[i].z > MaxZ) MaxZ = Path[i].z;
            }
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            if (Path == null || Path.Count < 2) return false;
            float totalR = HalfWidth + margin;
            if (p.x < MinX - totalR || p.x > MaxX + totalR ||
                p.z < MinZ - totalR || p.z > MaxZ + totalR)
                return false;

            float totalRSqr = totalR * totalR;
            for (int i = 0; i < Path.Count - 1; i++)
            {
                if (GeometryUtils.DistancePointToSegmentSqr2D(p, Path[i], Path[i + 1]) <= totalRSqr)
                    return true;
            }
            return false;
        }
    }
}
