using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using GeoCity3D.Visuals;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates truly solid building meshes — sealed extrusions.
    /// Outer walls + flat roof cap + flat bottom cap = watertight geometry.
    /// No inner cavity, no hollow shells.
    /// </summary>
    public class BuildingBuilder
    {
        // ── Public API ──

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

            // Ensure consistent winding (counter-clockwise when viewed from above)
            if (PolygonArea(footprint) < 0)
                footprint.Reverse();

            float height = DetermineHeight(way, area);
            string buildingType = (way.GetTag("building") ?? "").ToLower();
            bool isPitchedRoof = ShouldHavePitchedRoof(buildingType, height);
            bool hasSetback = height > 15f && area > 60f;

            return CreateSolidBuilding(footprint, height, wallMat, roofMat,
                wallUVOffset, wallUVScale, roofUVOffset, roofUVScale,
                way.Id, isPitchedRoof, hasSetback);
        }

        public static GameObject Build(OsmWay way, OsmData data,
            Material wallMat, Material roofMat, OriginShifter originShifter)
        {
            return Build(way, data, wallMat, roofMat,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.one,
                originShifter);
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID BUILDING — sealed extrusion, no hollow interior
        // ══════════════════════════════════════════════════════════════

        private static GameObject CreateSolidBuilding(List<Vector3> footprint, float height,
            Material wallMat, Material roofMat,
            Vector2 wOff, Vector2 wScl, Vector2 rOff, Vector2 rScl,
            long id, bool pitchedRoof, bool hasSetback)
        {
            GameObject go = new GameObject($"Building_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.materials = new Material[] { wallMat, roofMat };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

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
                mainHeight = height * 0.6f;
                setbackHeight = height - mainHeight;
                upperFootprint = ShrinkPolygon(footprint, 1.5f);
            }

            // ── OUTER WALLS of lower section ──
            AddSolidWalls(footprint, 0f, mainHeight, wOff, wScl, verts, uvs, wallTris);

            // ── Architectural detail on lower section ──
            AddBasePlinth(footprint, 0f, verts, uvs, wallTris);
            AddFloorLedges(footprint, 0f, mainHeight, verts, uvs, wallTris);
            AddWindowRecesses(footprint, 0f, mainHeight, verts, uvs, wallTris);

            // ── BOTTOM CAP (face down — seals the base, sits flush on ground) ──
            float minX, maxX, minZ, maxZ;
            ComputeBounds(footprint, out minX, out maxX, out minZ, out maxZ);
            AddSolidCap(footprint, 0f, minX, maxX, minZ, maxZ, verts, uvs, wallTris, true);

            if (hasSetback && upperFootprint != null && upperFootprint.Count >= 3)
            {
                // Ensure consistent winding on setback
                if (PolygonArea2D(upperFootprint) < 0)
                    upperFootprint.Reverse();

                // ── Terrace cap at main height (face up) ──
                AddSolidCap(footprint, mainHeight, minX, maxX, minZ, maxZ, verts, uvs, roofTris, false);

                // ── Upper section walls + detail ──
                AddSolidWalls(upperFootprint, mainHeight, setbackHeight, wOff, wScl, verts, uvs, wallTris);
                AddFloorLedges(upperFootprint, mainHeight, setbackHeight, verts, uvs, wallTris);
                AddWindowRecesses(upperFootprint, mainHeight, setbackHeight, verts, uvs, wallTris);

                // ── Upper roof ──
                float uMinX, uMaxX, uMinZ, uMaxZ;
                ComputeBounds(upperFootprint, out uMinX, out uMaxX, out uMinZ, out uMaxZ);
                float topY = mainHeight + setbackHeight;

                // Cornice at the top
                AddCornice(upperFootprint, topY, verts, uvs, wallTris);

                if (pitchedRoof)
                {
                    AddPitchedRoof(upperFootprint, topY, rOff, rScl, uMinX, uMaxX, uMinZ, uMaxZ, verts, uvs, roofTris);
                }
                else
                {
                    // Solid flat roof cap (face up) + parapet
                    AddSolidCap(upperFootprint, topY, uMinX, uMaxX, uMinZ, uMaxZ, verts, uvs, roofTris, false);
                    if (setbackHeight > 3f)
                        AddSolidParapet(upperFootprint, topY, 0.5f, wOff, wScl, verts, uvs, wallTris, roofTris);
                }
            }
            else
            {
                // ── Single volume — top cap + optional parapet ──
                // Cornice at the roof line
                if (!pitchedRoof && mainHeight > 4f)
                    AddCornice(footprint, mainHeight, verts, uvs, wallTris);

                if (pitchedRoof)
                {
                    AddPitchedRoof(footprint, mainHeight, rOff, rScl, minX, maxX, minZ, maxZ, verts, uvs, roofTris);
                }
                else
                {
                    // Solid flat roof (seals the top of the extrusion)
                    AddSolidCap(footprint, mainHeight, minX, maxX, minZ, maxZ, verts, uvs, roofTris, false);
                    if (height > 5f)
                        AddSolidParapet(footprint, mainHeight, 0.5f, wOff, wScl, verts, uvs, wallTris, roofTris);
                }
            }

            // ── Assemble mesh ──
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wallTris, 0);
            mesh.SetTriangles(roofTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;

            // Tight-fitting collider (DISABLED by default for massive city performance)
            // MeshCollider col = go.AddComponent<MeshCollider>();
            // col.sharedMesh = mesh;
            // col.convex = false;

            return go;
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID WALLS — one outward-facing quad per footprint edge
        // ══════════════════════════════════════════════════════════════

        private static void AddSolidWalls(List<Vector3> footprint, float baseY, float wallHeight,
            Vector2 wOff, Vector2 wScl,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            float cumDist = 0f;

            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];
                float segLen = Vector3.Distance(
                    new Vector3(p1.x, 0, p1.z),
                    new Vector3(p2.x, 0, p2.z));

                float topY = baseY + wallHeight;

                int bi = verts.Count;

                // Four corners of this wall quad
                verts.Add(new Vector3(p1.x, baseY, p1.z));   // bottom-left
                verts.Add(new Vector3(p2.x, baseY, p2.z));   // bottom-right
                verts.Add(new Vector3(p2.x, topY, p2.z));    // top-right
                verts.Add(new Vector3(p1.x, topY, p1.z));    // top-left

                // UV mapping
                float u1 = cumDist / 4f;
                float u2 = (cumDist + segLen) / 4f;
                float v1 = baseY / 3.2f;
                float v2 = topY / 3.2f;
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u2, v1));
                uvs.Add(new Vector2(u2, v2));
                uvs.Add(new Vector2(u1, v2));

                // Two triangles — outward-facing (CCW winding viewed from outside)
                tris.Add(bi + 0); tris.Add(bi + 2); tris.Add(bi + 1);
                tris.Add(bi + 0); tris.Add(bi + 3); tris.Add(bi + 2);

                cumDist += segLen;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID CAP — fills the entire polygon face (top or bottom)
        // ══════════════════════════════════════════════════════════════

        private static void AddSolidCap(List<Vector3> footprint, float capY,
            float minX, float maxX, float minZ, float maxZ,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool faceDown)
        {
            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);

            int baseIdx = verts.Count;
            for (int i = 0; i < footprint.Count; i++)
            {
                verts.Add(new Vector3(footprint[i].x, capY, footprint[i].z));
                uvs.Add(new Vector2(
                    (footprint[i].x - minX) / sizeX,
                    (footprint[i].z - minZ) / sizeZ));
            }

            // Triangulate using XZ projection
            List<Vector3> flatPts = new List<Vector3>();
            for (int i = 0; i < footprint.Count; i++)
                flatPts.Add(new Vector3(footprint[i].x, 0, footprint[i].z));

            List<int> capTris = GeometryUtils.Triangulate(flatPts);

            if (faceDown)
            {
                // Reverse winding for downward-facing cap
                for (int i = capTris.Count - 1; i >= 0; i--)
                    tris.Add(baseIdx + capTris[i]);
            }
            else
            {
                for (int i = 0; i < capTris.Count; i++)
                    tris.Add(baseIdx + capTris[i]);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID PARAPET — a small sealed box ring around the roof edge
        // ══════════════════════════════════════════════════════════════

        private static void AddSolidParapet(List<Vector3> footprint, float roofY, float parapetH,
            Vector2 wOff, Vector2 wScl,
            List<Vector3> verts, List<Vector2> uvs, List<int> wallTris, List<int> roofTris)
        {
            List<Vector3> inner = ShrinkPolygon(footprint, 0.2f);
            if (inner == null || inner.Count < 3) return;

            // Ensure consistent winding
            if (PolygonArea2D(inner) < 0)
                inner.Reverse();

            float topY = roofY + parapetH;

            // Outer walls of parapet (face outward)
            AddSolidWalls(footprint, roofY, parapetH, wOff, wScl, verts, uvs, wallTris);

            // Inner walls of parapet (face inward — reverse winding)
            for (int i = 0; i < inner.Count; i++)
            {
                Vector3 p1 = inner[i];
                Vector3 p2 = inner[(i + 1) % inner.Count];

                int bi = verts.Count;
                verts.Add(new Vector3(p1.x, roofY, p1.z));
                verts.Add(new Vector3(p2.x, roofY, p2.z));
                verts.Add(new Vector3(p2.x, topY, p2.z));
                verts.Add(new Vector3(p1.x, topY, p1.z));

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));

                // Reverse winding — faces inward
                wallTris.Add(bi + 0); wallTris.Add(bi + 1); wallTris.Add(bi + 2);
                wallTris.Add(bi + 0); wallTris.Add(bi + 2); wallTris.Add(bi + 3);
            }

            // Top cap of parapet (horizontal strip between outer and inner edges)
            int count = Mathf.Min(footprint.Count, inner.Count);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int bi = verts.Count;

                verts.Add(new Vector3(footprint[i].x, topY, footprint[i].z));
                verts.Add(new Vector3(footprint[next].x, topY, footprint[next].z));
                verts.Add(new Vector3(inner[next].x, topY, inner[next].z));
                verts.Add(new Vector3(inner[i].x, topY, inner[i].z));

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));

                // Face up
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);
            }
        }

        // ── Pitched Roof ──

        private static void AddPitchedRoof(List<Vector3> footprint, float roofBaseY,
            Vector2 rOff, Vector2 rScl, float minX, float maxX, float minZ, float maxZ,
            List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);
            float ridgeHeight = Mathf.Min(sizeX, sizeZ) * 0.3f;
            ridgeHeight = Mathf.Clamp(ridgeHeight, 1.5f, 4f);

            float peakY = roofBaseY + ridgeHeight;
            float centerX = (minX + maxX) / 2f;
            float centerZ = (minZ + maxZ) / 2f;
            Vector3 peak = new Vector3(centerX, peakY, centerZ);

            int peakIdx = verts.Count;
            verts.Add(peak);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int baseIdx = verts.Count;
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 v = new Vector3(footprint[i].x, roofBaseY, footprint[i].z);
                verts.Add(v);
                uvs.Add(new Vector2(
                    (footprint[i].x - minX) / sizeX,
                    (footprint[i].z - minZ) / sizeZ));
            }

            for (int i = 0; i < footprint.Count; i++)
            {
                int next = (i + 1) % footprint.Count;
                tris.Add(baseIdx + i);
                tris.Add(peakIdx);
                tris.Add(baseIdx + next);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  FLOOR LEDGES — thin horizontal bands every ~3.2m (one floor)
        //  Creates shadow-catching geometry that shows floor separation
        // ══════════════════════════════════════════════════════════════

        private static void AddFloorLedges(List<Vector3> footprint, float baseY, float totalHeight,
            List<Vector3> verts, List<Vector2> uvs, List<int> wallTris)
        {
            float floorHeight = 3.2f;
            float ledgeDepth = 0.12f;  // How far the ledge sticks out
            float ledgeThickness = 0.15f; // Vertical thickness of the band

            int floors = Mathf.FloorToInt(totalHeight / floorHeight);
            if (floors < 2) return; // No ledges for single-floor

            for (int floor = 1; floor < floors; floor++)
            {
                float ledgeY = baseY + floor * floorHeight;
                float ledgeTop = ledgeY + ledgeThickness * 0.5f;
                float ledgeBot = ledgeY - ledgeThickness * 0.5f;

                for (int i = 0; i < footprint.Count; i++)
                {
                    Vector3 p1 = footprint[i];
                    Vector3 p2 = footprint[(i + 1) % footprint.Count];

                    // Wall outward normal (2D) — for CCW footprint
                    Vector3 wallDir = (p2 - p1).normalized;
                    Vector3 outward = new Vector3(wallDir.z, 0, -wallDir.x) * ledgeDepth;

                    // Outer corners of the ledge
                    Vector3 ob1 = new Vector3(p1.x, ledgeBot, p1.z) + outward;
                    Vector3 ob2 = new Vector3(p2.x, ledgeBot, p2.z) + outward;
                    Vector3 ot1 = new Vector3(p1.x, ledgeTop, p1.z) + outward;
                    Vector3 ot2 = new Vector3(p2.x, ledgeTop, p2.z) + outward;

                    // Inner corners (on the wall surface)
                    Vector3 ib1 = new Vector3(p1.x, ledgeBot, p1.z);
                    Vector3 ib2 = new Vector3(p2.x, ledgeBot, p2.z);
                    Vector3 it1 = new Vector3(p1.x, ledgeTop, p1.z);
                    Vector3 it2 = new Vector3(p2.x, ledgeTop, p2.z);

                    // Front face (outward-facing)
                    int bi = verts.Count;
                    verts.Add(ob1); verts.Add(ob2); verts.Add(ot2); verts.Add(ot1);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                    wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                    // Top face (face up — catches light)
                    bi = verts.Count;
                    verts.Add(it1); verts.Add(it2); verts.Add(ot2); verts.Add(ot1);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                    wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                    // Bottom face (face down — creates shadow)
                    bi = verts.Count;
                    verts.Add(ib1); verts.Add(ib2); verts.Add(ob2); verts.Add(ob1);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    wallTris.Add(bi); wallTris.Add(bi + 1); wallTris.Add(bi + 2);
                    wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 3);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  BASE PLINTH — wider band at ground level (0 to ~1m)
        //  Grounds the building visually
        // ══════════════════════════════════════════════════════════════

        private static void AddBasePlinth(List<Vector3> footprint, float baseY,
            List<Vector3> verts, List<Vector2> uvs, List<int> wallTris)
        {
            float plinthHeight = 0.8f;
            float plinthDepth = 0.1f;

            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];

                Vector3 wallDir = (p2 - p1).normalized;
                Vector3 outward = new Vector3(wallDir.z, 0, -wallDir.x) * plinthDepth;

                Vector3 ob1 = new Vector3(p1.x, baseY, p1.z) + outward;
                Vector3 ob2 = new Vector3(p2.x, baseY, p2.z) + outward;
                Vector3 ot1 = new Vector3(p1.x, baseY + plinthHeight, p1.z) + outward;
                Vector3 ot2 = new Vector3(p2.x, baseY + plinthHeight, p2.z) + outward;

                // Front face
                int bi = verts.Count;
                verts.Add(ob1); verts.Add(ob2); verts.Add(ot2); verts.Add(ot1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                // Top face (lip of the plinth)
                Vector3 it1 = new Vector3(p1.x, baseY + plinthHeight, p1.z);
                Vector3 it2 = new Vector3(p2.x, baseY + plinthHeight, p2.z);
                bi = verts.Count;
                verts.Add(it1); verts.Add(it2); verts.Add(ot2); verts.Add(ot1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CORNICE — decorative overhang at rooftop edge
        //  Creates a crown molding effect
        // ══════════════════════════════════════════════════════════════

        private static void AddCornice(List<Vector3> footprint, float roofY,
            List<Vector3> verts, List<Vector2> uvs, List<int> wallTris)
        {
            float corniceDepth = 0.2f;
            float corniceHeight = 0.3f;

            float corniceBot = roofY - corniceHeight;

            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];

                Vector3 wallDir = (p2 - p1).normalized;
                Vector3 outward = new Vector3(wallDir.z, 0, -wallDir.x) * corniceDepth;

                // Outer overhang corners
                Vector3 ob1 = new Vector3(p1.x, corniceBot, p1.z) + outward;
                Vector3 ob2 = new Vector3(p2.x, corniceBot, p2.z) + outward;
                Vector3 ot1 = new Vector3(p1.x, roofY, p1.z) + outward;
                Vector3 ot2 = new Vector3(p2.x, roofY, p2.z) + outward;

                // Front face of cornice
                int bi = verts.Count;
                verts.Add(ob1); verts.Add(ob2); verts.Add(ot2); verts.Add(ot1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                // Bottom face (underside of overhang — visible from below)
                Vector3 ib1 = new Vector3(p1.x, corniceBot, p1.z);
                Vector3 ib2 = new Vector3(p2.x, corniceBot, p2.z);
                bi = verts.Count;
                verts.Add(ib1); verts.Add(ib2); verts.Add(ob2); verts.Add(ob1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                wallTris.Add(bi); wallTris.Add(bi + 1); wallTris.Add(bi + 2);
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 3);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  WINDOW RECESSES — indented rectangles on each wall face
        //  Creates realistic shadow-casting window openings
        // ══════════════════════════════════════════════════════════════

        private static void AddWindowRecesses(List<Vector3> footprint, float baseY, float totalHeight,
            List<Vector3> verts, List<Vector2> uvs, List<int> wallTris)
        {
            float floorHeight = 3.2f;
            float recessDepth = 0.08f;    // How deep windows are indented
            float windowWidth = 1.2f;     // Window width in meters
            float windowHeight = 1.6f;    // Window height
            float windowBottom = 0.9f;    // Sill height from floor
            float windowSpacing = 2.8f;   // Center-to-center distance between windows

            int floors = Mathf.FloorToInt(totalHeight / floorHeight);
            if (floors < 1) return;

            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];

                float wallLen = Vector3.Distance(
                    new Vector3(p1.x, 0, p1.z),
                    new Vector3(p2.x, 0, p2.z));

                if (wallLen < windowSpacing) continue; // Wall too short for windows

                Vector3 wallDir = (p2 - p1).normalized;
                Vector3 inward = new Vector3(-wallDir.z, 0, wallDir.x) * recessDepth;

                // How many windows fit on this wall
                int winCount = Mathf.FloorToInt((wallLen - 1.0f) / windowSpacing);
                if (winCount < 1) continue;

                float startOffset = (wallLen - winCount * windowSpacing) * 0.5f + windowSpacing * 0.5f;

                for (int floor = 0; floor < floors; floor++)
                {
                    float floorBase = baseY + floor * floorHeight;

                    for (int w = 0; w < winCount; w++)
                    {
                        float centerDist = startOffset + w * windowSpacing;

                        // Window center on the wall
                        Vector3 wCenter = p1 + wallDir * centerDist;

                        // Window rectangle corners (indented into the wall)
                        float halfW = windowWidth * 0.5f;
                        float winBot = floorBase + windowBottom;
                        float winTop = winBot + windowHeight;

                        Vector3 bl = wCenter - wallDir * halfW + inward;
                        bl.y = winBot;
                        Vector3 br = wCenter + wallDir * halfW + inward;
                        br.y = winBot;
                        Vector3 tr = wCenter + wallDir * halfW + inward;
                        tr.y = winTop;
                        Vector3 tl = wCenter - wallDir * halfW + inward;
                        tl.y = winTop;

                        // Recessed face (the window "glass" — darker because it's indented)
                        int bi = verts.Count;
                        verts.Add(bl); verts.Add(br); verts.Add(tr); verts.Add(tl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                        wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                        // Sill (top of bottom reveal — horizontal face)
                        Vector3 sbl = wCenter - wallDir * halfW; sbl.y = winBot;
                        Vector3 sbr = wCenter + wallDir * halfW; sbr.y = winBot;
                        bi = verts.Count;
                        verts.Add(sbl); verts.Add(sbr); verts.Add(br); verts.Add(bl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 0.3f)); uvs.Add(new Vector2(0, 0.3f));
                        wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                        wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                        // Lintel (bottom of top reveal — face down)
                        Vector3 ltl = wCenter - wallDir * halfW; ltl.y = winTop;
                        Vector3 ltr = wCenter + wallDir * halfW; ltr.y = winTop;
                        bi = verts.Count;
                        verts.Add(ltl); verts.Add(ltr); verts.Add(tr); verts.Add(tl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 0.3f)); uvs.Add(new Vector2(0, 0.3f));
                        wallTris.Add(bi); wallTris.Add(bi + 1); wallTris.Add(bi + 2);
                        wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 3);

                        // Left jamb (side reveal)
                        Vector3 jbl = wCenter - wallDir * halfW; jbl.y = winBot;
                        Vector3 jtl2 = wCenter - wallDir * halfW; jtl2.y = winTop;
                        bi = verts.Count;
                        verts.Add(jbl); verts.Add(bl); verts.Add(tl); verts.Add(jtl2);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0.3f, 0));
                        uvs.Add(new Vector2(0.3f, 1)); uvs.Add(new Vector2(0, 1));
                        wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                        wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                        // Right jamb
                        Vector3 jbr = wCenter + wallDir * halfW; jbr.y = winBot;
                        Vector3 jtr2 = wCenter + wallDir * halfW; jtr2.y = winTop;
                        bi = verts.Count;
                        verts.Add(br); verts.Add(jbr); verts.Add(jtr2); verts.Add(tr);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0.3f, 0));
                        uvs.Add(new Vector2(0.3f, 1)); uvs.Add(new Vector2(0, 1));
                        wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                        wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════

        private static List<Vector3> ExtractFootprint(OsmWay way, OsmData data, OriginShifter originShifter)
        {
            List<Vector3> footprint = new List<Vector3>();
            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                    footprint.Add(originShifter.GetLocalPosition(node.Latitude, node.Longitude));
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

        private static float PolygonArea2D(List<Vector3> pts)
        {
            float a = 0f;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                a += (pts[j].x + pts[i].x) * (pts[j].z - pts[i].z);
            return a * 0.5f;
        }

        private static bool ShouldHavePitchedRoof(string type, float height)
        {
            if (height > 8f) return false;
            switch (type)
            {
                case "house":
                case "detached": return Random.value > 0.5f;
                default: return false;
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

        private static List<Vector3> ShrinkPolygon(List<Vector3> polygon, float amount)
        {
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < polygon.Count; i++)
                centroid += polygon[i];
            centroid /= polygon.Count;

            List<Vector3> shrunk = new List<Vector3>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 dir = (centroid - polygon[i]).normalized;
                float dist = Vector3.Distance(polygon[i], centroid);
                float moveAmount = Mathf.Min(amount, dist * 0.4f);
                shrunk.Add(polygon[i] + dir * moveAmount);
            }
            return shrunk;
        }

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
