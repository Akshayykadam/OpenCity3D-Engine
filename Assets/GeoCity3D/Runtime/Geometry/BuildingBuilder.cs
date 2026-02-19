using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using GeoCity3D.Visuals;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class BuildingBuilder
    {
        /// <summary>
        /// Build using atlas-based UV mapping (shared materials).
        /// </summary>
        public static GameObject Build(OsmWay way, OsmData data,
            Material wallMat, Material roofMat,
            Vector2 wallUVOffset, Vector2 wallUVScale,
            Vector2 roofUVOffset, Vector2 roofUVScale,
            OriginShifter originShifter)
        {
            List<Vector3> footprint = ExtractFootprint(way, data, originShifter);
            if (footprint == null) return null;

            float area = Mathf.Abs(PolygonArea(footprint));
            if (area < 4f) return null;

            float height = DetermineHeight(way, area);
            string buildingType = (way.GetTag("building") ?? "").ToLower();
            bool isPitchedRoof = ShouldHavePitchedRoof(buildingType, height);
            bool hasSetback = height > 15f && area > 60f;

            return CreateMesh(footprint, height, wallMat, roofMat,
                wallUVOffset, wallUVScale, roofUVOffset, roofUVScale,
                way.Id, isPitchedRoof, hasSetback);
        }

        /// <summary>
        /// Backward-compatible build without atlas (uses direct materials).
        /// </summary>
        public static GameObject Build(OsmWay way, OsmData data, Material wallMat, Material roofMat, OriginShifter originShifter)
        {
            return Build(way, data, wallMat, roofMat,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.one,
                originShifter);
        }

        private static List<Vector3> ExtractFootprint(OsmWay way, OsmData data, OriginShifter originShifter)
        {
            List<Vector3> footprint = new List<Vector3>();

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    footprint.Add(originShifter.GetLocalPosition(node.Latitude, node.Longitude));
                }
            }

            if (footprint.Count < 3) return null;

            if (Vector3.Distance(footprint[0], footprint[footprint.Count - 1]) < 0.1f)
                footprint.RemoveAt(footprint.Count - 1);

            return footprint.Count < 3 ? null : footprint;
        }

        private static float PolygonArea(List<Vector3> pts)
        {
            float a = 0f;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                a += (pts[j].x + pts[i].x) * (pts[j].z - pts[i].z);
            return a * 0.5f;
        }

        private static bool ShouldHavePitchedRoof(string type, float height)
        {
            // In India, most buildings have flat concrete roofs.
            // Only small explicitly-tagged houses get pitched roofs.
            if (height > 8f) return false;
            switch (type)
            {
                case "house":
                case "detached":
                    return Random.value > 0.5f; // Even houses, only 50% get pitched
                default:
                    return false;
            }
        }

        private static float DetermineHeight(OsmWay way, float footprintArea)
        {
            if (way.Tags.ContainsKey("height"))
            {
                if (float.TryParse(way.Tags["height"].Replace("m", ""),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float h))
                    return Mathf.Max(h, 3f);
            }

            if (way.Tags.ContainsKey("building:levels"))
            {
                if (int.TryParse(way.Tags["building:levels"], out int levels))
                    return Mathf.Max(levels * 3.2f, 3f);
            }

            string type = (way.GetTag("building") ?? "").ToLower();
            float baseH;
            switch (type)
            {
                case "apartments": baseH = Random.Range(12f, 20f); break;
                case "residential": baseH = Random.Range(8f, 14f); break;
                case "commercial":
                case "office": baseH = Random.Range(14f, 28f); break;
                case "industrial":
                case "warehouse": baseH = Random.Range(6f, 10f); break;
                case "church":
                case "cathedral": baseH = Random.Range(12f, 20f); break;
                case "garage":
                case "shed":
                case "hut": baseH = Random.Range(3f, 5f); break;
                case "house":
                case "detached": baseH = Random.Range(6f, 10f); break;
                default: baseH = Random.Range(6f, 14f); break;
            }

            if (footprintArea < 30f) baseH = Mathf.Min(baseH, 8f);
            else if (footprintArea < 80f) baseH = Mathf.Min(baseH, 14f);

            return baseH;
        }

        // ── Mesh Creation ──

        private static GameObject CreateMesh(List<Vector3> footprint, float height,
            Material wallMat, Material roofMat,
            Vector2 wOff, Vector2 wScl, Vector2 rOff, Vector2 rScl,
            long id, bool pitchedRoof, bool hasSetback)
        {
            GameObject go = new GameObject($"Building_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.materials = new Material[] { wallMat, roofMat };

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> wallTris = new List<int>();
            List<int> roofTris = new List<int>();

            float mainHeight = height;
            float setbackHeight = 0f;
            List<Vector3> upperFootprint = null;

            if (hasSetback)
            {
                // Lower portion = 60% of height, upper portion = remaining 40% on shrunk footprint
                mainHeight = height * 0.6f;
                setbackHeight = height - mainHeight;
                upperFootprint = ShrinkPolygon(footprint, 1.5f);
            }

            // ── Lower walls (with floor ledges) ──
            BuildWallsWithLedges(footprint, 0f, mainHeight, wOff, wScl, verts, uvs, wallTris);

            // ── Lower roof / setback terrace ──
            float minX, maxX, minZ, maxZ;
            ComputeBounds(footprint, out minX, out maxX, out minZ, out maxZ);

            if (hasSetback && upperFootprint != null && upperFootprint.Count >= 3)
            {
                // Flat terrace on the setback
                AddFlatCap(footprint, mainHeight, rOff, rScl, minX, maxX, minZ, maxZ, verts, uvs, roofTris, false);

                // ── Upper walls (setback) ──
                BuildWallsWithLedges(upperFootprint, mainHeight, setbackHeight, wOff, wScl, verts, uvs, wallTris);

                // ── Upper roof ──
                float uMinX, uMaxX, uMinZ, uMaxZ;
                ComputeBounds(upperFootprint, out uMinX, out uMaxX, out uMinZ, out uMaxZ);

                if (pitchedRoof)
                    AddPitchedRoof(upperFootprint, mainHeight + setbackHeight, rOff, rScl, uMinX, uMaxX, uMinZ, uMaxZ, verts, uvs, roofTris);
                else
                    AddFlatCap(upperFootprint, mainHeight + setbackHeight, rOff, rScl, uMinX, uMaxX, uMinZ, uMaxZ, verts, uvs, roofTris, false);
            }
            else
            {
                // No setback — just add roof
                if (pitchedRoof)
                    AddPitchedRoof(footprint, mainHeight, rOff, rScl, minX, maxX, minZ, maxZ, verts, uvs, roofTris);
                else
                    AddFlatCap(footprint, mainHeight, rOff, rScl, minX, maxX, minZ, maxZ, verts, uvs, roofTris, false);
            }

            // ── Bottom cap ──
            AddFlatCap(footprint, 0f, rOff, rScl, minX, maxX, minZ, maxZ, verts, uvs, wallTris, true);

            // ── Assemble ──
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wallTris, 0);
            mesh.SetTriangles(roofTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;

            BoxCollider col = go.AddComponent<BoxCollider>();
            Bounds bounds = mesh.bounds;
            col.center = bounds.center;
            col.size = bounds.size;

            return go;
        }

        // ── Wall Generation with Floor Ledges ──

        private static void BuildWallsWithLedges(List<Vector3> footprint, float baseY, float wallHeight,
            Vector2 wOff, Vector2 wScl,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            float floorHeight = 3.2f;
            float ledgeDepth = 0.08f;
            float ledgeHeight = 0.15f;

            int numFloors = Mathf.FloorToInt(wallHeight / floorHeight);

            float cumDist = 0f;
            // UV scale: 1 texture tile = 1 window bay = 4m wide × 3.2m tall
            float BAY_WIDTH = 4.0f;
            float BAY_HEIGHT = 3.2f;
            float uScale = 1f / BAY_WIDTH;   // U repeats every 4m
            float vScale = 1f / BAY_HEIGHT;   // V repeats every 3.2m

            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];
                float segLen = Vector3.Distance(new Vector3(p1.x, 0, p1.z), new Vector3(p2.x, 0, p2.z));

                Vector3 dir = (p2 - p1);
                dir.y = 0;
                dir.Normalize();
                Vector3 outward = Vector3.Cross(Vector3.up, dir).normalized;

                // Build wall segments with ledge breaks at each floor
                float currentY = baseY;
                for (int floor = 0; floor <= numFloors; floor++)
                {
                    float nextFloorY;
                    if (floor < numFloors)
                        nextFloorY = baseY + (floor + 1) * floorHeight;
                    else
                        nextFloorY = baseY + wallHeight; // Top of wall

                    if (nextFloorY <= currentY) break;

                    float ledgeY = nextFloorY;

                    // Main wall section (below ledge)
                    float wallTopY = (floor < numFloors) ? ledgeY - ledgeHeight : nextFloorY;
                    if (wallTopY > currentY)
                    {
                        AddWallQuad(p1, p2, currentY, wallTopY, Vector3.zero,
                            cumDist, segLen, uScale, vScale, wOff, wScl, verts, uvs, tris);
                    }

                    // Ledge strip (only between floors, not at the very top)
                    if (floor < numFloors && wallHeight > floorHeight * 1.5f)
                    {
                        float lBottom = ledgeY - ledgeHeight;
                        float lTop = ledgeY;

                        // Ledge face (pushed outward)
                        AddWallQuad(p1, p2, lBottom, lTop, outward * ledgeDepth,
                            cumDist, segLen, uScale, vScale, wOff, wScl, verts, uvs, tris);

                        // Ledge top face (small horizontal strip)
                        int bi = verts.Count;
                        verts.Add(p1 + Vector3.up * lTop);
                        verts.Add(p2 + Vector3.up * lTop);
                        verts.Add(p2 + Vector3.up * lTop + outward * ledgeDepth);
                        verts.Add(p1 + Vector3.up * lTop + outward * ledgeDepth);
                        uvs.Add(RemapUV(new Vector2(cumDist * uScale, lTop * vScale), wOff, wScl));
                        uvs.Add(RemapUV(new Vector2((cumDist + segLen) * uScale, lTop * vScale), wOff, wScl));
                        uvs.Add(RemapUV(new Vector2((cumDist + segLen) * uScale, (lTop + 0.1f) * vScale), wOff, wScl));
                        uvs.Add(RemapUV(new Vector2(cumDist * uScale, (lTop + 0.1f) * vScale), wOff, wScl));
                        tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                        tris.Add(bi); tris.Add(bi + 3); tris.Add(bi + 2);
                    }

                    currentY = nextFloorY;
                }

                cumDist += segLen;
            }
        }

        private static void AddWallQuad(Vector3 p1, Vector3 p2, float botY, float topY,
            Vector3 offset, float cumDist, float segLen, float uScale, float vScale,
            Vector2 wOff, Vector2 wScl,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            int bi = verts.Count;
            verts.Add(p1 + Vector3.up * botY + offset);
            verts.Add(p2 + Vector3.up * botY + offset);
            verts.Add(p2 + Vector3.up * topY + offset);
            verts.Add(p1 + Vector3.up * topY + offset);

            uvs.Add(RemapUV(new Vector2(cumDist * uScale, botY * vScale), wOff, wScl));
            uvs.Add(RemapUV(new Vector2((cumDist + segLen) * uScale, botY * vScale), wOff, wScl));
            uvs.Add(RemapUV(new Vector2((cumDist + segLen) * uScale, topY * vScale), wOff, wScl));
            uvs.Add(RemapUV(new Vector2(cumDist * uScale, topY * vScale), wOff, wScl));

            tris.Add(bi + 0); tris.Add(bi + 2); tris.Add(bi + 1);
            tris.Add(bi + 0); tris.Add(bi + 3); tris.Add(bi + 2);
        }

        // ── Pitched Roof ──

        private static void AddPitchedRoof(List<Vector3> footprint, float roofBaseY,
            Vector2 rOff, Vector2 rScl, float minX, float maxX, float minZ, float maxZ,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);

            // Determine ridge direction along longest axis
            bool ridgeAlongX = sizeX >= sizeZ;
            float ridgeHeight = Mathf.Min(sizeX, sizeZ) * 0.3f; // Roof pitch ~30% of shorter side
            ridgeHeight = Mathf.Clamp(ridgeHeight, 1.5f, 4f);

            float peakY = roofBaseY + ridgeHeight;

            // Compute ridge line center
            float centerX = (minX + maxX) / 2f;
            float centerZ = (minZ + maxZ) / 2f;

            // For simplicity, create a hip-style roof with a center peak
            // All footprint edges connect to the peak point
            Vector3 peak = new Vector3(centerX, peakY, centerZ);

            int peakIdx = verts.Count;
            verts.Add(peak);
            uvs.Add(RemapRoofUV(peak, minX, maxX, minZ, maxZ, rOff, rScl));

            // Add footprint top vertices
            int baseIdx = verts.Count;
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 v = footprint[i] + Vector3.up * roofBaseY;
                verts.Add(v);
                uvs.Add(RemapRoofUV(v, minX, maxX, minZ, maxZ, rOff, rScl));
            }

            // Create triangular faces from each edge to the peak
            for (int i = 0; i < footprint.Count; i++)
            {
                int next = (i + 1) % footprint.Count;
                tris.Add(baseIdx + i);
                tris.Add(peakIdx);
                tris.Add(baseIdx + next);
            }
        }

        // ── Flat Cap ──

        private static void AddFlatCap(List<Vector3> footprint, float capY,
            Vector2 rOff, Vector2 rScl, float minX, float maxX, float minZ, float maxZ,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool flipWinding)
        {
            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);

            int baseIdx = verts.Count;
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 v = footprint[i] + Vector3.up * capY;
                verts.Add(v);
                uvs.Add(new Vector2(
                    rOff.x + ((footprint[i].x - minX) / sizeX) * rScl.x,
                    rOff.y + ((footprint[i].z - minZ) / sizeZ) * rScl.y));
            }

            // Use same footprint at Y=0 for triangulation (XZ plane)
            List<Vector3> flatPts = new List<Vector3>();
            for (int i = 0; i < footprint.Count; i++)
                flatPts.Add(new Vector3(footprint[i].x, 0, footprint[i].z));

            List<int> capTris = GeometryUtils.Triangulate(flatPts);

            if (flipWinding)
            {
                for (int i = capTris.Count - 1; i >= 0; i--)
                    tris.Add(baseIdx + capTris[i]);
            }
            else
            {
                for (int i = 0; i < capTris.Count; i++)
                    tris.Add(baseIdx + capTris[i]);
            }
        }

        // ── Polygon Shrink (for setbacks) ──

        private static List<Vector3> ShrinkPolygon(List<Vector3> polygon, float amount)
        {
            // Simple inward offset by moving each vertex toward the centroid
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < polygon.Count; i++)
                centroid += polygon[i];
            centroid /= polygon.Count;

            List<Vector3> shrunk = new List<Vector3>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 dir = (centroid - polygon[i]).normalized;
                float dist = Vector3.Distance(polygon[i], centroid);
                float moveAmount = Mathf.Min(amount, dist * 0.4f); // Don't shrink more than 40%
                shrunk.Add(polygon[i] + dir * moveAmount);
            }

            return shrunk;
        }

        // ── UV Helpers ──

        private static Vector2 RemapUV(Vector2 localUV, Vector2 offset, Vector2 scale)
        {
            // For wall atlas: remap local UV into atlas tile region
            // The local UV wraps via frac(), then we offset into the atlas tile
            return new Vector2(
                offset.x + Frac(localUV.x) * scale.x,
                offset.y + Frac(localUV.y) * scale.y
            );
        }

        private static Vector2 RemapRoofUV(Vector3 worldPos, float minX, float maxX, float minZ, float maxZ,
            Vector2 offset, Vector2 scale)
        {
            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);
            return new Vector2(
                offset.x + ((worldPos.x - minX) / sizeX) * scale.x,
                offset.y + ((worldPos.z - minZ) / sizeZ) * scale.y
            );
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        private static void ComputeBounds(List<Vector3> pts,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue; maxX = float.MinValue;
            minZ = float.MaxValue; maxZ = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].x < minX) minX = pts[i].x;
                if (pts[i].x > maxX) maxX = pts[i].x;
                if (pts[i].z < minZ) minZ = pts[i].z;
                if (pts[i].z > maxZ) maxZ = pts[i].z;
            }
        }
    }
}
