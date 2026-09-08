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
            OriginShifter originShifter, Material windowMat = null)
        {
            List<Vector3> footprint = ExtractFootprint(way, data, originShifter);
            if (footprint == null) return null;

            float area = Mathf.Abs(PolygonArea(footprint));
            if (area < 4f) return null;

            // Ensure consistent winding (counter-clockwise when viewed from above)
            if (PolygonArea(footprint) < 0)
                footprint.Reverse();

            // Round corners for sleek, modern, identical procedural structures
            footprint = RoundFootprintCorners(footprint, 1.8f, 3);
            if (footprint == null || footprint.Count < 3) return null;

            if (PolygonArea(footprint) < 0)
                footprint.Reverse();

            float minHeight = DetermineMinHeight(way);
            float totalHeight = DetermineHeight(way, area);
            float topY = Mathf.Max(minHeight + 3f, totalHeight);
            float wallHeight = topY - minHeight;

            // Identical structural profile: modern flat roof with uniform parapet & cornice
            bool isPitchedRoof = false;
            bool hasSetback = false;

            return CreateSolidBuilding(footprint, wallHeight, wallMat, roofMat,
                wallUVOffset, wallUVScale, roofUVOffset, roofUVScale,
                way.Id, isPitchedRoof, hasSetback, minHeight, windowMat);
        }

        public static GameObject Build(OsmWay way, OsmData data,
            Material wallMat, Material roofMat, OriginShifter originShifter, Material windowMat = null)
        {
            return Build(way, data, wallMat, roofMat,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.one,
                originShifter, windowMat);
        }

        /// <summary>
        /// Directly build a procedural building from an arbitrary 2D/3D polygon footprint with rounded corners.
        /// </summary>
        public static GameObject BuildFromFootprint(List<Vector3> rawFootprint, float height,
            Material wallMat, Material roofMat, long id, float cornerRadius = 1.8f, Material windowMat = null)
        {
            if (rawFootprint == null || rawFootprint.Count < 3) return null;

            List<Vector3> footprint = new List<Vector3>(rawFootprint);

            if (PolygonArea(footprint) < 0)
                footprint.Reverse();

            footprint = RoundFootprintCorners(footprint, cornerRadius, 3);
            if (footprint == null || footprint.Count < 3) return null;

            if (PolygonArea(footprint) < 0)
                footprint.Reverse();

            return CreateSolidBuilding(footprint, height, wallMat, roofMat,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.one,
                id, false, false, 0f, windowMat);
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID BUILDING — sealed extrusion, no hollow interior
        // ══════════════════════════════════════════════════════════════

        private static Material _defaultGlassMat;
        private static Material GetDefaultGlassMaterial(Shader fallbackShader)
        {
            if (_defaultGlassMat != null) return _defaultGlassMat;
            Shader shader = fallbackShader;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null) return null;

            _defaultGlassMat = new Material(shader);
            _defaultGlassMat.name = "BuildingGlass_Default";
            Color glassColor = new Color(0.12f, 0.20f, 0.32f, 1.0f);
            _defaultGlassMat.color = glassColor;
            if (_defaultGlassMat.HasProperty("_BaseColor")) _defaultGlassMat.SetColor("_BaseColor", glassColor);
            if (_defaultGlassMat.HasProperty("_Color")) _defaultGlassMat.SetColor("_Color", glassColor);
            if (_defaultGlassMat.HasProperty("_Smoothness")) _defaultGlassMat.SetFloat("_Smoothness", 0.92f);
            if (_defaultGlassMat.HasProperty("_Glossiness")) _defaultGlassMat.SetFloat("_Glossiness", 0.92f);
            if (_defaultGlassMat.HasProperty("_Metallic")) _defaultGlassMat.SetFloat("_Metallic", 0.50f);
            if (_defaultGlassMat.HasProperty("_Cull")) _defaultGlassMat.SetFloat("_Cull", 0f);
            _defaultGlassMat.renderQueue = 2000;
            _defaultGlassMat.enableInstancing = true;
            return _defaultGlassMat;
        }

        private static GameObject CreateSolidBuilding(List<Vector3> footprint, float height,
            Material wallMat, Material roofMat,
            Vector2 wOff, Vector2 wScl, Vector2 rOff, Vector2 rScl,
            long id, bool pitchedRoof, bool hasSetback, float baseY = 0f, Material windowMat = null)
        {
            if (windowMat == null)
                windowMat = GetDefaultGlassMaterial(wallMat != null ? wallMat.shader : null);

            GameObject go = new GameObject($"Building_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = windowMat != null 
                ? new Material[] { wallMat, roofMat, windowMat } 
                : new Material[] { wallMat, roofMat };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Allow large skyscraper meshes (>65k vertices)
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> wallTris = new List<int>();
            List<int> roofTris = new List<int>();
            List<int> glassTris = new List<int>();

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
            AddSolidWalls(footprint, baseY, mainHeight, wOff, wScl, verts, uvs, colors, wallTris);

            // ── Architectural detail on lower section ──
            if (baseY < 0.1f)
            {
                AddBasePlinth(footprint, baseY, verts, uvs, colors, wallTris);
            }
            AddFloorLedges(footprint, baseY, mainHeight, verts, uvs, colors, wallTris);
            AddWindowFeatures(footprint, baseY, mainHeight, verts, uvs, colors, wallTris, glassTris);

            // ── BOTTOM CAP (face down — seals the base) ──
            float minX, maxX, minZ, maxZ;
            ComputeBounds(footprint, out minX, out maxX, out minZ, out maxZ);
            AddSolidCap(footprint, baseY, minX, maxX, minZ, maxZ, verts, uvs, colors, wallTris, true);

            if (hasSetback && upperFootprint != null && upperFootprint.Count >= 3)
            {
                // Ensure consistent winding on setback
                if (PolygonArea2D(upperFootprint) < 0)
                    upperFootprint.Reverse();

                // ── Terrace cap at main height (face up) ──
                AddSolidCap(footprint, baseY + mainHeight, minX, maxX, minZ, maxZ, verts, uvs, colors, roofTris, false);

                // ── Upper section walls + detail ──
                AddSolidWalls(upperFootprint, baseY + mainHeight, setbackHeight, wOff, wScl, verts, uvs, colors, wallTris);
                AddFloorLedges(upperFootprint, baseY + mainHeight, setbackHeight, verts, uvs, colors, wallTris);
                AddWindowFeatures(upperFootprint, baseY + mainHeight, setbackHeight, verts, uvs, colors, wallTris, glassTris);

                // ── Upper roof ──
                float uMinX, uMaxX, uMinZ, uMaxZ;
                ComputeBounds(upperFootprint, out uMinX, out uMaxX, out uMinZ, out uMaxZ);
                float topY = baseY + mainHeight + setbackHeight;

                // Cornice at the top
                AddCornice(upperFootprint, topY, verts, uvs, colors, wallTris);

                if (pitchedRoof)
                {
                    AddPitchedRoof(upperFootprint, topY, rOff, rScl, uMinX, uMaxX, uMinZ, uMaxZ, verts, uvs, colors, roofTris);
                }
                else
                {
                    // Solid flat roof cap (face up) + parapet
                    AddSolidCap(upperFootprint, topY, uMinX, uMaxX, uMinZ, uMaxZ, verts, uvs, colors, roofTris, false);
                    if (setbackHeight > 3f)
                        AddSolidParapet(upperFootprint, topY, 0.5f, wOff, wScl, verts, uvs, colors, wallTris, roofTris);
                }
            }
            else
            {
                // ── Single volume — top cap + optional parapet ──
                float topY = baseY + mainHeight;
                if (!pitchedRoof && mainHeight > 4f)
                    AddCornice(footprint, topY, verts, uvs, colors, wallTris);

                if (pitchedRoof)
                {
                    AddPitchedRoof(footprint, topY, rOff, rScl, minX, maxX, minZ, maxZ, verts, uvs, colors, roofTris);
                }
                else
                {
                    // Solid flat roof (seals the top of the extrusion)
                    AddSolidCap(footprint, topY, minX, maxX, minZ, maxZ, verts, uvs, colors, roofTris, false);
                    if (height > 5f)
                        AddSolidParapet(footprint, topY, 0.5f, wOff, wScl, verts, uvs, colors, wallTris, roofTris);
                }
            }

            // ── Assemble mesh ──
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            if (windowMat != null && glassTris.Count > 0)
            {
                mesh.subMeshCount = 3;
                mesh.SetTriangles(wallTris, 0);
                mesh.SetTriangles(roofTris, 1);
                mesh.SetTriangles(glassTris, 2);
            }
            else
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(wallTris, 0);
                mesh.SetTriangles(roofTris, 1);
            }
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
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris)
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

                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);

                // UV mapping — tiles 4m wide by 3.2m tall (matches 1 story facade panel)
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
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris, bool faceDown)
        {
            if (footprint == null || footprint.Count < 3) return;

            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);

            int baseIdx = verts.Count;
            Color capColor = faceDown ? Color.white : new Color(0.88f, 0.88f, 0.88f);

            for (int i = 0; i < footprint.Count; i++)
            {
                verts.Add(new Vector3(footprint[i].x, capY, footprint[i].z));
                uvs.Add(new Vector2(
                    (footprint[i].x - minX) / sizeX,
                    (footprint[i].z - minZ) / sizeZ));
                colors.Add(capColor);
            }

            // Triangulate using XZ projection
            List<Vector3> flatPts = new List<Vector3>();
            for (int i = 0; i < footprint.Count; i++)
                flatPts.Add(new Vector3(footprint[i].x, 0, footprint[i].z));

            List<int> capTris = GeometryUtils.Triangulate(flatPts);

            if (capTris != null && capTris.Count >= 3)
            {
                if (faceDown)
                {
                    // Reverse winding for downward-facing bottom cap
                    for (int i = capTris.Count - 1; i >= 0; i--)
                        tris.Add(baseIdx + capTris[i]);
                }
                else
                {
                    // Upward-facing top roof cap with double-sided winding guarantee so roof is never culled
                    for (int i = 0; i < capTris.Count; i += 3)
                    {
                        if (i + 2 < capTris.Count)
                        {
                            int a = baseIdx + capTris[i];
                            int b = baseIdx + capTris[i + 1];
                            int c = baseIdx + capTris[i + 2];
                            // Primary upward winding
                            tris.Add(a); tris.Add(b); tris.Add(c);
                            // Reverse winding guarantee
                            tris.Add(a); tris.Add(c); tris.Add(b);
                        }
                    }
                }
            }
            else
            {
                // Robust centroid fan triangulation fallback: guarantees solid top even on complex or self-intersecting polygons
                Vector3 center = Vector3.zero;
                for (int i = 0; i < footprint.Count; i++)
                    center += footprint[i];
                center /= footprint.Count;
                center.y = capY;

                int centerIdx = verts.Count;
                verts.Add(center);
                uvs.Add(new Vector2(0.5f, 0.5f));
                colors.Add(capColor);

                for (int i = 0; i < footprint.Count; i++)
                {
                    int next = (i + 1) % footprint.Count;
                    int p0 = centerIdx;
                    int p1 = baseIdx + i;
                    int p2 = baseIdx + next;

                    if (faceDown)
                    {
                        tris.Add(p0); tris.Add(p2); tris.Add(p1);
                    }
                    else
                    {
                        // Double-sided fan guarantee
                        tris.Add(p0); tris.Add(p1); tris.Add(p2);
                        tris.Add(p0); tris.Add(p2); tris.Add(p1);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SOLID PARAPET — a small sealed box ring around the roof edge
        // ══════════════════════════════════════════════════════════════

        private static void AddSolidParapet(List<Vector3> footprint, float roofY, float parapetH,
            Vector2 wOff, Vector2 wScl,
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> wallTris, List<int> roofTris)
        {
            List<Vector3> inner = ShrinkPolygon(footprint, 0.2f);
            if (inner == null || inner.Count < 3) return;

            // Ensure consistent winding
            if (PolygonArea2D(inner) < 0)
                inner.Reverse();

            float topY = roofY + parapetH;
            Color parapetColor = new Color(0.80f, 0.80f, 0.82f);

            // Outer walls of parapet (face outward)
            AddSolidWalls(footprint, roofY, parapetH, wOff, wScl, verts, uvs, colors, wallTris);

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

                colors.Add(parapetColor);
                colors.Add(parapetColor);
                colors.Add(parapetColor);
                colors.Add(parapetColor);

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

                colors.Add(parapetColor);
                colors.Add(parapetColor);
                colors.Add(parapetColor);
                colors.Add(parapetColor);

                // Face up
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);
            }
        }

        // ── Pitched Roof ──

        private static void AddPitchedRoof(List<Vector3> footprint, float roofBaseY,
            Vector2 rOff, Vector2 rScl, float minX, float maxX, float minZ, float maxZ,
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris)
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
            colors.Add(Color.white);

            int baseIdx = verts.Count;
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 v = new Vector3(footprint[i].x, roofBaseY, footprint[i].z);
                verts.Add(v);
                uvs.Add(new Vector2(
                    (footprint[i].x - minX) / sizeX,
                    (footprint[i].z - minZ) / sizeZ));
                colors.Add(Color.white);
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
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> wallTris)
        {
            float floorHeight = 3.2f;
            float ledgeDepth = 0.12f;  // How far the ledge sticks out
            float ledgeThickness = 0.15f; // Vertical thickness of the band

            int floors = Mathf.FloorToInt(totalHeight / floorHeight);
            if (floors < 2) return; // No ledges for single-floor

            Vector3[] miters = ComputeOutwardVertexMiters(footprint, ledgeDepth);
            Color ledgeColor = new Color(0.82f, 0.84f, 0.86f);

            for (int floor = 1; floor < floors; floor++)
            {
                float ledgeY = baseY + floor * floorHeight;
                float ledgeTop = ledgeY + ledgeThickness * 0.5f;
                float ledgeBot = ledgeY - ledgeThickness * 0.5f;

                for (int i = 0; i < footprint.Count; i++)
                {
                    int next = (i + 1) % footprint.Count;
                    Vector3 p1 = footprint[i];
                    Vector3 p2 = footprint[next];

                    Vector3 out1 = miters[i];
                    Vector3 out2 = miters[next];

                    // Outer corners of the ledge
                    Vector3 ob1 = new Vector3(p1.x, ledgeBot, p1.z) + out1;
                    Vector3 ob2 = new Vector3(p2.x, ledgeBot, p2.z) + out2;
                    Vector3 ot1 = new Vector3(p1.x, ledgeTop, p1.z) + out1;
                    Vector3 ot2 = new Vector3(p2.x, ledgeTop, p2.z) + out2;

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
                    colors.Add(ledgeColor); colors.Add(ledgeColor); colors.Add(ledgeColor); colors.Add(ledgeColor);
                    wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                    wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                    // Top face (face up — catches light)
                    bi = verts.Count;
                    verts.Add(it1); verts.Add(it2); verts.Add(ot2); verts.Add(ot1);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    colors.Add(ledgeColor * 1.1f); colors.Add(ledgeColor * 1.1f); colors.Add(ledgeColor * 1.1f); colors.Add(ledgeColor * 1.1f);
                    wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                    wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                    // Bottom face (face down — creates shadow)
                    bi = verts.Count;
                    verts.Add(ib1); verts.Add(ib2); verts.Add(ob2); verts.Add(ob1);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    colors.Add(ledgeColor * 0.75f); colors.Add(ledgeColor * 0.75f); colors.Add(ledgeColor * 0.75f); colors.Add(ledgeColor * 0.75f);
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
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> wallTris)
        {
            float plinthHeight = 0.8f;
            float plinthDepth = 0.1f;

            Vector3[] miters = ComputeOutwardVertexMiters(footprint, plinthDepth);
            Color plinthColor = new Color(0.62f, 0.64f, 0.66f);

            for (int i = 0; i < footprint.Count; i++)
            {
                int next = (i + 1) % footprint.Count;
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[next];

                Vector3 out1 = miters[i];
                Vector3 out2 = miters[next];

                Vector3 ob1 = new Vector3(p1.x, baseY, p1.z) + out1;
                Vector3 ob2 = new Vector3(p2.x, baseY, p2.z) + out2;
                Vector3 ot1 = new Vector3(p1.x, baseY + plinthHeight, p1.z) + out1;
                Vector3 ot2 = new Vector3(p2.x, baseY + plinthHeight, p2.z) + out2;

                // Front face
                int bi = verts.Count;
                verts.Add(ob1); verts.Add(ob2); verts.Add(ot2); verts.Add(ot1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                colors.Add(plinthColor); colors.Add(plinthColor); colors.Add(plinthColor); colors.Add(plinthColor);
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                // Top face (lip of the plinth)
                Vector3 it1 = new Vector3(p1.x, baseY + plinthHeight, p1.z);
                Vector3 it2 = new Vector3(p2.x, baseY + plinthHeight, p2.z);
                bi = verts.Count;
                verts.Add(it1); verts.Add(it2); verts.Add(ot2); verts.Add(ot1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                colors.Add(plinthColor * 1.1f); colors.Add(plinthColor * 1.1f); colors.Add(plinthColor * 1.1f); colors.Add(plinthColor * 1.1f);
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CORNICE — decorative overhang at rooftop edge
        //  Creates a crown molding effect
        // ══════════════════════════════════════════════════════════════

        private static void AddCornice(List<Vector3> footprint, float roofY,
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> wallTris)
        {
            float corniceDepth = 0.2f;
            float corniceHeight = 0.3f;
            float corniceBot = roofY - corniceHeight;

            Vector3[] miters = ComputeOutwardVertexMiters(footprint, corniceDepth);
            Color corniceColor = new Color(0.85f, 0.86f, 0.88f);

            for (int i = 0; i < footprint.Count; i++)
            {
                int next = (i + 1) % footprint.Count;
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[next];

                Vector3 out1 = miters[i];
                Vector3 out2 = miters[next];

                // Outer overhang corners
                Vector3 ob1 = new Vector3(p1.x, corniceBot, p1.z) + out1;
                Vector3 ob2 = new Vector3(p2.x, corniceBot, p2.z) + out2;
                Vector3 ot1 = new Vector3(p1.x, roofY, p1.z) + out1;
                Vector3 ot2 = new Vector3(p2.x, roofY, p2.z) + out2;

                // Front face of cornice
                int bi = verts.Count;
                verts.Add(ob1); verts.Add(ob2); verts.Add(ot2); verts.Add(ot1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                colors.Add(corniceColor); colors.Add(corniceColor); colors.Add(corniceColor); colors.Add(corniceColor);
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 1);
                wallTris.Add(bi); wallTris.Add(bi + 3); wallTris.Add(bi + 2);

                // Bottom face (underside of overhang — visible from below)
                Vector3 ib1 = new Vector3(p1.x, corniceBot, p1.z);
                Vector3 ib2 = new Vector3(p2.x, corniceBot, p2.z);
                bi = verts.Count;
                verts.Add(ib1); verts.Add(ib2); verts.Add(ob2); verts.Add(ob1);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                colors.Add(corniceColor * 0.75f); colors.Add(corniceColor * 0.75f); colors.Add(corniceColor * 0.75f); colors.Add(corniceColor * 0.75f);
                wallTris.Add(bi); wallTris.Add(bi + 1); wallTris.Add(bi + 2);
                wallTris.Add(bi); wallTris.Add(bi + 2); wallTris.Add(bi + 3);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  WINDOW FEATURES — 3D frames, stone sills, mullions & reflective glass
        //  Creates crisp architectural definition in normal Shaded mode
        // ══════════════════════════════════════════════════════════════

        private static void AddWindowFeatures(List<Vector3> footprint, float baseY, float totalHeight,
            List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> wallTris, List<int> glassTris)
        {
            float floorHeight = 3.2f;
            float windowWidth = 1.3f;     // Window width
            float windowHeight = 1.6f;    // Window height
            float windowBottom = 0.85f;   // Sill height above floor
            float windowSpacing = 2.6f;   // Center-to-center distance

            int floors = Mathf.FloorToInt(totalHeight / floorHeight);
            if (floors < 1) return;

            Color frameColor = new Color(0.18f, 0.20f, 0.23f); // Dark charcoal architectural frame / mullions
            Color sillColor = new Color(0.72f, 0.74f, 0.76f);  // Stone sill trim
            Color glassColor = new Color(0.25f, 0.38f, 0.52f); // Reflective blue glass

            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];

                float wallLen = Vector3.Distance(new Vector3(p1.x, 0, p1.z), new Vector3(p2.x, 0, p2.z));
                if (wallLen < windowSpacing) continue; // Wall too short for windows

                Vector3 wallDir = (p2 - p1).normalized;
                // Outward normal in 2D (counter-clockwise footprint)
                Vector3 outward = new Vector3(wallDir.z, 0, -wallDir.x);

                int winCount = Mathf.FloorToInt((wallLen - 0.8f) / windowSpacing);
                if (winCount < 1) continue;

                float startOffset = (wallLen - winCount * windowSpacing) * 0.5f + windowSpacing * 0.5f;

                for (int floor = 0; floor < floors; floor++)
                {
                    float floorBase = baseY + floor * floorHeight;

                    for (int w = 0; w < winCount; w++)
                    {
                        float centerDist = startOffset + w * windowSpacing;
                        Vector3 wCenter = p1 + wallDir * centerDist;

                        float halfW = windowWidth * 0.5f;
                        float winBot = floorBase + windowBottom;
                        float winTop = winBot + windowHeight;

                        // ── 1. REFLECTIVE GLASS PANE (Submesh 2) ──
                        // Slightly in front of wall quad to prevent Z-fighting and wall occlusion
                        Vector3 gOffset = outward * 0.015f;
                        Vector3 gbl = wCenter - wallDir * halfW + gOffset; gbl.y = winBot;
                        Vector3 gbr = wCenter + wallDir * halfW + gOffset; gbr.y = winBot;
                        Vector3 gtr = wCenter + wallDir * halfW + gOffset; gtr.y = winTop;
                        Vector3 gtl = wCenter - wallDir * halfW + gOffset; gtl.y = winTop;

                        int gi = verts.Count;
                        verts.Add(gbl); verts.Add(gbr); verts.Add(gtr); verts.Add(gtl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(glassColor); colors.Add(glassColor); colors.Add(glassColor); colors.Add(glassColor);
                        glassTris.Add(gi); glassTris.Add(gi + 2); glassTris.Add(gi + 1);
                        glassTris.Add(gi); glassTris.Add(gi + 3); glassTris.Add(gi + 2);

                        // ── 2. PROTRUDING WINDOW SILL (Submesh 0) ──
                        // Thick stone shelf at bottom of window that catches light and casts shadows
                        float sillExt = 0.07f; // Sticking out 7cm
                        float sillH = 0.08f;   // 8cm thick
                        float sillW = halfW + 0.08f;
                        Vector3 sOffset = outward * sillExt;
                        float sTop = winBot;
                        float sBot = winBot - sillH;

                        Vector3 s_obl = wCenter - wallDir * sillW + sOffset; s_obl.y = sBot;
                        Vector3 s_obr = wCenter + wallDir * sillW + sOffset; s_obr.y = sBot;
                        Vector3 s_otr = wCenter + wallDir * sillW + sOffset; s_otr.y = sTop;
                        Vector3 s_otl = wCenter - wallDir * sillW + sOffset; s_otl.y = sTop;

                        Vector3 s_ibl = wCenter - wallDir * sillW; s_ibl.y = sBot;
                        Vector3 s_ibr = wCenter + wallDir * sillW; s_ibr.y = sBot;
                        Vector3 s_itr = wCenter + wallDir * sillW; s_itr.y = sTop;
                        Vector3 s_itl = wCenter - wallDir * sillW; s_itl.y = sTop;

                        // Sill front face
                        int si = verts.Count;
                        verts.Add(s_obl); verts.Add(s_obr); verts.Add(s_otr); verts.Add(s_otl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(sillColor); colors.Add(sillColor); colors.Add(sillColor); colors.Add(sillColor);
                        wallTris.Add(si); wallTris.Add(si + 2); wallTris.Add(si + 1);
                        wallTris.Add(si); wallTris.Add(si + 3); wallTris.Add(si + 2);

                        // Sill top face (catches light)
                        si = verts.Count;
                        verts.Add(s_itl); verts.Add(s_itr); verts.Add(s_otr); verts.Add(s_otl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(sillColor * 1.15f); colors.Add(sillColor * 1.15f); colors.Add(sillColor * 1.15f); colors.Add(sillColor * 1.15f);
                        wallTris.Add(si); wallTris.Add(si + 2); wallTris.Add(si + 1);
                        wallTris.Add(si); wallTris.Add(si + 3); wallTris.Add(si + 2);

                        // Sill bottom face (casts shadow)
                        si = verts.Count;
                        verts.Add(s_ibl); verts.Add(s_ibr); verts.Add(s_obr); verts.Add(s_obl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(sillColor * 0.7f); colors.Add(sillColor * 0.7f); colors.Add(sillColor * 0.7f); colors.Add(sillColor * 0.7f);
                        wallTris.Add(si); wallTris.Add(si + 1); wallTris.Add(si + 2);
                        wallTris.Add(si); wallTris.Add(si + 2); wallTris.Add(si + 3);

                        // ── 3. WINDOW FRAME & LINTEL HEADER (Submesh 0) ──
                        // Dark charcoal architectural trim framing the opening
                        float frameExt = 0.045f;
                        float frameThick = 0.06f;
                        Vector3 fOffset = outward * frameExt;

                        // Top lintel bar
                        Vector3 l_bl = wCenter - wallDir * (halfW + frameThick) + fOffset; l_bl.y = winTop;
                        Vector3 l_br = wCenter + wallDir * (halfW + frameThick) + fOffset; l_br.y = winTop;
                        Vector3 l_tr = wCenter + wallDir * (halfW + frameThick) + fOffset; l_tr.y = winTop + frameThick;
                        Vector3 l_tl = wCenter - wallDir * (halfW + frameThick) + fOffset; l_tl.y = winTop + frameThick;

                        int fi = verts.Count;
                        verts.Add(l_bl); verts.Add(l_br); verts.Add(l_tr); verts.Add(l_tl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor);
                        wallTris.Add(fi); wallTris.Add(fi + 2); wallTris.Add(fi + 1);
                        wallTris.Add(fi); wallTris.Add(fi + 3); wallTris.Add(fi + 2);

                        // Left frame jamb
                        Vector3 j1_bl = wCenter - wallDir * (halfW + frameThick) + fOffset; j1_bl.y = winBot;
                        Vector3 j1_br = wCenter - wallDir * halfW + fOffset; j1_br.y = winBot;
                        Vector3 j1_tr = wCenter - wallDir * halfW + fOffset; j1_tr.y = winTop;
                        Vector3 j1_tl = wCenter - wallDir * (halfW + frameThick) + fOffset; j1_tl.y = winTop;

                        fi = verts.Count;
                        verts.Add(j1_bl); verts.Add(j1_br); verts.Add(j1_tr); verts.Add(j1_tl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor);
                        wallTris.Add(fi); wallTris.Add(fi + 2); wallTris.Add(fi + 1);
                        wallTris.Add(fi); wallTris.Add(fi + 3); wallTris.Add(fi + 2);

                        // Right frame jamb
                        Vector3 j2_bl = wCenter + wallDir * halfW + fOffset; j2_bl.y = winBot;
                        Vector3 j2_br = wCenter + wallDir * (halfW + frameThick) + fOffset; j2_br.y = winBot;
                        Vector3 j2_tr = wCenter + wallDir * (halfW + frameThick) + fOffset; j2_tr.y = winTop;
                        Vector3 j2_tl = wCenter + wallDir * halfW + fOffset; j2_tl.y = winTop;

                        fi = verts.Count;
                        verts.Add(j2_bl); verts.Add(j2_br); verts.Add(j2_tr); verts.Add(j2_tl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor);
                        wallTris.Add(fi); wallTris.Add(fi + 2); wallTris.Add(fi + 1);
                        wallTris.Add(fi); wallTris.Add(fi + 3); wallTris.Add(fi + 2);

                        // ── 4. VERTICAL CENTER MULLION (Submesh 0) ──
                        // Dark structural divider down the middle of the window pane
                        float mullW = 0.035f;
                        Vector3 mOffset = outward * 0.035f;
                        Vector3 m_bl = wCenter - wallDir * mullW + mOffset; m_bl.y = winBot;
                        Vector3 m_br = wCenter + wallDir * mullW + mOffset; m_br.y = winBot;
                        Vector3 m_tr = wCenter + wallDir * mullW + mOffset; m_tr.y = winTop;
                        Vector3 m_tl = wCenter - wallDir * mullW + mOffset; m_tl.y = winTop;

                        fi = verts.Count;
                        verts.Add(m_bl); verts.Add(m_br); verts.Add(m_tr); verts.Add(m_tl);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                        colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor); colors.Add(frameColor);
                        wallTris.Add(fi); wallTris.Add(fi + 2); wallTris.Add(fi + 1);
                        wallTris.Add(fi); wallTris.Add(fi + 3); wallTris.Add(fi + 2);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════════

        public static List<Vector3> ExtractFootprint(OsmWay way, OsmData data, OriginShifter originShifter)
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

        public static float PolygonArea(List<Vector3> pts)
        {
            float a = 0f;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                a += pts[j].x * pts[i].z - pts[i].x * pts[j].z;
            return a * 0.5f;
        }

        public static Vector3 ComputeCentroid(List<Vector3> pts)
        {
            if (pts == null || pts.Count == 0) return Vector3.zero;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
                c += pts[i];
            c /= pts.Count;
            return c;
        }

        private static float PolygonArea2D(List<Vector3> pts)
        {
            float a = 0f;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                a += pts[j].x * pts[i].z - pts[i].x * pts[j].z;
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

        public static float DetermineMinHeight(OsmWay way)
        {
            if (way.Tags == null) return 0f;

            string minHStr = way.GetTag("min_height") ?? way.GetTag("building:min_height");
            if (!string.IsNullOrEmpty(minHStr))
            {
                if (float.TryParse(minHStr.Replace("m", "").Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float h))
                {
                    return Mathf.Max(0f, h);
                }
            }

            string minLStr = way.GetTag("min_level") ?? way.GetTag("building:min_level");
            if (!string.IsNullOrEmpty(minLStr))
            {
                if (float.TryParse(minLStr.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float lvl))
                {
                    return Mathf.Max(0f, lvl * 3.2f);
                }
            }

            return 0f;
        }

        public static float DetermineHeight(OsmWay way, float footprintArea)
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

        /// <summary>
        /// Rounds the corners of a footprint polygon by replacing sharp corners with smooth quadratic Bezier arc vertices.
        /// Safety clamping prevents corner crossovers and degenerate geometry.
        /// </summary>
        public static List<Vector3> RoundFootprintCorners(List<Vector3> footprint, float targetRadius = 1.8f, int arcSegments = 3)
        {
            if (footprint == null || footprint.Count < 3) return footprint;

            // 1. Clean near-duplicate vertices
            List<Vector3> clean = new List<Vector3>();
            for (int i = 0; i < footprint.Count; i++)
            {
                if (clean.Count == 0 || Vector3.Distance(new Vector3(footprint[i].x, 0, footprint[i].z),
                                                         new Vector3(clean[clean.Count - 1].x, 0, clean[clean.Count - 1].z)) > 0.2f)
                {
                    clean.Add(footprint[i]);
                }
            }
            if (clean.Count >= 3 && Vector3.Distance(new Vector3(clean[0].x, 0, clean[0].z),
                                                     new Vector3(clean[clean.Count - 1].x, 0, clean[clean.Count - 1].z)) <= 0.2f)
            {
                clean.RemoveAt(clean.Count - 1);
            }
            if (clean.Count < 3) return footprint;

            int n = clean.Count;
            List<Vector3> result = new List<Vector3>();

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = clean[(i - 1 + n) % n];
                Vector3 curr = clean[i];
                Vector3 next = clean[(i + 1) % n];

                Vector3 vPrev = new Vector3(prev.x - curr.x, 0, prev.z - curr.z);
                Vector3 vNext = new Vector3(next.x - curr.x, 0, next.z - curr.z);

                float lenPrev = vPrev.magnitude;
                float lenNext = vNext.magnitude;

                // If either adjacent edge is very short, keep original corner without rounding
                if (lenPrev < 0.6f || lenNext < 0.6f)
                {
                    result.Add(curr);
                    continue;
                }

                Vector3 dirPrev = vPrev / lenPrev;
                Vector3 dirNext = vNext / lenNext;

                float dot = Vector3.Dot(dirPrev, dirNext);

                // Skip flat lines (angle ~180°) or extreme spikes (angle < 15°)
                if (dot < -0.98f || dot > 0.96f)
                {
                    result.Add(curr);
                    continue;
                }

                // Clamped corner offset: max 38% of adjacent edge lengths
                float s = Mathf.Min(targetRadius, lenPrev * 0.38f, lenNext * 0.38f);
                if (s < 0.25f)
                {
                    result.Add(curr);
                    continue;
                }

                Vector3 pStart = curr + dirPrev * s;
                Vector3 pEnd = curr + dirNext * s;
                pStart.y = curr.y;
                pEnd.y = curr.y;

                result.Add(pStart);

                // Quadratic Bezier arc interpolation between pStart and pEnd with control point curr
                for (int k = 1; k < arcSegments; k++)
                {
                    float t = (float)k / arcSegments;
                    float omt = 1f - t;
                    Vector3 p = omt * omt * pStart + 2f * omt * t * curr + t * t * pEnd;
                    p.y = curr.y;
                    result.Add(p);
                }

                result.Add(pEnd);
            }

            // Post-clean duplicate vertices
            List<Vector3> finalResult = new List<Vector3>();
            for (int i = 0; i < result.Count; i++)
            {
                if (finalResult.Count == 0 || Vector3.Distance(new Vector3(result[i].x, 0, result[i].z),
                                                               new Vector3(finalResult[finalResult.Count - 1].x, 0, finalResult[finalResult.Count - 1].z)) > 0.1f)
                {
                    finalResult.Add(result[i]);
                }
            }
            if (finalResult.Count >= 3 && Vector3.Distance(new Vector3(finalResult[0].x, 0, finalResult[0].z),
                                                           new Vector3(finalResult[finalResult.Count - 1].x, 0, finalResult[finalResult.Count - 1].z)) <= 0.1f)
            {
                finalResult.RemoveAt(finalResult.Count - 1);
            }

            // Fallback safety check
            if (finalResult.Count < 3 || Mathf.Abs(PolygonArea2D(finalResult)) < 2f)
                return footprint;

            return finalResult;
        }

        private static Vector3[] ComputeOutwardVertexMiters(List<Vector3> footprint, float depth)
        {
            int n = footprint.Count;
            Vector3[] miters = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = footprint[(i - 1 + n) % n];
                Vector3 curr = footprint[i];
                Vector3 next = footprint[(i + 1) % n];

                Vector3 d1 = new Vector3(curr.x - prev.x, 0, curr.z - prev.z).normalized;
                Vector3 d2 = new Vector3(next.x - curr.x, 0, next.z - curr.z).normalized;

                // Outward 2D normals for CCW footprint
                Vector3 n1 = new Vector3(d1.z, 0, -d1.x);
                Vector3 n2 = new Vector3(d2.z, 0, -d2.x);

                Vector3 avgN = (n1 + n2).normalized;
                if (avgN.sqrMagnitude < 0.001f) avgN = n1;

                float cosAngle = Vector3.Dot(avgN, n1);
                float miterScale = 1f;
                if (cosAngle > 0.35f)
                    miterScale = Mathf.Min(1f / cosAngle, 1.4f);

                miters[i] = avgN * (depth * miterScale);
            }
            return miters;
        }
    }
}
