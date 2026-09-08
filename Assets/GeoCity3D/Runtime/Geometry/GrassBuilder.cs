using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates stylized low-poly 3D grass tufts, clusters, and ground carpets for parks, riverbanks, and green land.
    /// Supports both multi-faceted procedural mesh generation (with shared materials for CityCombiner batching)
    /// and FBX prefab instantiation.
    /// </summary>
    public static class GrassBuilder
    {
        // ── Rich Green Color Palette (Curated to pop vibrantly against the lime-green ground) ──
        // ── Rich Green Color Palette (Curated to pop vibrantly with rich contrast against ground) ──
        private static readonly Color[] GrassColors = new Color[]
        {
            new Color(0.12f, 0.44f, 0.08f), // Deep rich meadow emerald
            new Color(0.18f, 0.52f, 0.12f), // Lush park green
            new Color(0.08f, 0.36f, 0.06f), // Dark forest shade green
            new Color(0.22f, 0.58f, 0.14f), // Vibrant spring lawn
            new Color(0.15f, 0.46f, 0.10f), // Olive rich grass
            new Color(0.26f, 0.62f, 0.16f), // Sun-drenched warm green
        };

        // Shared material pool for draw-call batching with CityCombiner
        private static Material[] _sharedGrassMats;

        public static void EnsureMaterialPool(Shader shader)
        {
            if (_sharedGrassMats != null && _sharedGrassMats.Length > 0 && _sharedGrassMats[0] != null) return;

            _sharedGrassMats = new Material[GrassColors.Length];
            for (int i = 0; i < GrassColors.Length; i++)
            {
                Material mat = new Material(shader);
                mat.name = $"GrassMat_{i}";

                // 1. Procedural grass blade texture (guarantees vibrant green in ALL render pipelines)
                Texture2D grassTex = CreateGrassTexture(GrassColors[i]);
                mat.mainTexture = grassTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", grassTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", grassTex);

                // 2. Primary color properties (URP _BaseColor + Standard _Color)
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

                // 3. Matte foliage surface: two-sided, non-metallic, zero glossy glare blowout
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.04f);
                if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                if (mat.HasProperty("_GlossyReflections")) mat.SetFloat("_GlossyReflections", 0f);

                mat.renderQueue = 2000;
                mat.enableInstancing = true;
                _sharedGrassMats[i] = mat;
            }
        }

        public static void ResetMaterialPool()
        {
            _sharedGrassMats = null;
        }

        /// <summary>
        /// Creates a procedural grass blade gradient texture for vibrant, organic blade appearance.
        /// </summary>
        private static Texture2D CreateGrassTexture(Color mainColor)
        {
            int w = 64, h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "GrassTexture";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color rootColor = Color.Lerp(mainColor, new Color(0.06f, 0.22f, 0.04f), 0.60f);
            Color tipColor = Color.Lerp(mainColor, new Color(0.46f, 0.86f, 0.18f), 0.40f);

            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float ty = (float)y / (h - 1);
                Color rowBase = Color.Lerp(rootColor, tipColor, ty);

                for (int x = 0; x < w; x++)
                {
                    float tx = (float)x / (w - 1);
                    float edge = 1f - Mathf.Pow(Mathf.Abs(tx - 0.5f) * 2f, 2f) * 0.15f;
                    float stripe = Mathf.Sin(tx * 25f) * 0.04f;
                    Color p = rowBase * (edge + stripe);
                    p.a = 1f;
                    px[y * w + x] = p;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// Generates a rich, stylized low-poly 3D grass clump composed of multi-faceted, outward-curving blades.
        /// Scaled to be clearly visible from camera distances (0.9m - 1.8m tall).
        /// </summary>
        public static GameObject BuildProceduralTuft(Vector3 position, Shader shader, float scale = 1f)
        {
            EnsureMaterialPool(shader);

            int matIdx = Random.Range(0, _sharedGrassMats.Length);
            Color chosenColor = GrassColors[matIdx];

            GameObject go = new GameObject("GrassTuft");
            go.transform.position = position;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedGrassMats[matIdx];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            // Generate 8 to 12 multi-faceted stylized blades arranged in layers (balanced for high density & 60+ FPS)
            int bladeCount = Random.Range(8, 13);
            float angleStep = 360f / bladeCount;

            for (int b = 0; b < bladeCount; b++)
            {
                float angle = (b * angleStep + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;

                // Substantial dimensions visible across wide city distances
                float bladeHeight = Random.Range(0.85f, 1.65f) * scale;
                float baseWidth = Random.Range(0.24f, 0.38f) * scale;
                float midWidth = baseWidth * 0.72f;
                float midHeight = bladeHeight * 0.55f;
                float midLean = Random.Range(0.12f, 0.25f) * scale;
                float tipLean = Random.Range(0.28f, 0.55f) * scale;

                // Base vertices
                Vector3 b0 = -side * (baseWidth / 2f);
                Vector3 b1 = side * (baseWidth / 2f);

                // Mid crease vertices
                Vector3 midCenter = dir * midLean + new Vector3(0f, midHeight, 0f);
                Vector3 m0 = midCenter - side * (midWidth / 2f);
                Vector3 m1 = midCenter + side * (midWidth / 2f);

                // Tip vertex
                Vector3 tip = dir * tipLean + new Vector3(0f, bladeHeight, 0f);

                // Upward-biased foliage dome normals (AAA industry standard for grass).
                // Front normal faces outward and upward; Back normal faces inward and upward.
                // Upward bias ensures both sides catch sky ambient and sun light smoothly without black backfaces or NaN zero-vectors!
                Vector3 nFront = (dir * 0.35f + Vector3.up * 0.65f).normalized;
                Vector3 nBack = (-dir * 0.35f + Vector3.up * 0.65f).normalized;

                // Vertex colors: dark root green -> vibrant mid green -> bright golden tip
                Color rootCol = Color.Lerp(chosenColor, new Color(0.06f, 0.22f, 0.04f), 0.60f);
                Color tipCol = Color.Lerp(chosenColor, new Color(0.46f, 0.85f, 0.18f), 0.40f);

                // ── 1. FRONT FACE (5 vertices) ──
                int fIdx = verts.Count;
                verts.Add(b0);
                verts.Add(b1);
                verts.Add(m0);
                verts.Add(m1);
                verts.Add(tip);

                for (int i = 0; i < 5; i++) normals.Add(nFront);

                colors.Add(rootCol);
                colors.Add(rootCol);
                colors.Add(chosenColor);
                colors.Add(chosenColor);
                colors.Add(tipCol);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0.15f, 0.55f));
                uvs.Add(new Vector2(0.85f, 0.55f));
                uvs.Add(new Vector2(0.5f, 1f));

                // Front quad (b0, b1, m1, m0)
                tris.Add(fIdx + 0); tris.Add(fIdx + 2); tris.Add(fIdx + 1);
                tris.Add(fIdx + 1); tris.Add(fIdx + 2); tris.Add(fIdx + 3);
                // Front upper triangle (m0, tip, m1)
                tris.Add(fIdx + 2); tris.Add(fIdx + 4); tris.Add(fIdx + 3);

                // ── 2. BACK FACE (5 vertices - separate indices, inverted winding & back normals) ──
                int bIdx = verts.Count;
                verts.Add(b0);
                verts.Add(b1);
                verts.Add(m0);
                verts.Add(m1);
                verts.Add(tip);

                for (int i = 0; i < 5; i++) normals.Add(nBack);

                colors.Add(rootCol);
                colors.Add(rootCol);
                colors.Add(chosenColor);
                colors.Add(chosenColor);
                colors.Add(tipCol);

                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0.85f, 0.55f));
                uvs.Add(new Vector2(0.15f, 0.55f));
                uvs.Add(new Vector2(0.5f, 1f));

                // Back quad (reversed winding)
                tris.Add(bIdx + 0); tris.Add(bIdx + 1); tris.Add(bIdx + 2);
                tris.Add(bIdx + 1); tris.Add(bIdx + 3); tris.Add(bIdx + 2);
                // Back upper triangle (reversed winding)
                tris.Add(bIdx + 2); tris.Add(bIdx + 3); tris.Add(bIdx + 4);
            }

            mesh.vertices = verts.ToArray();
            mesh.normals = normals.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            return go;
        }

        /// <summary>
        /// Spawns a grass prefab instance, auto-scaled and grounded.
        /// </summary>
        public static GameObject BuildPrefab(Vector3 position, GameObject[] grassPrefabs, float scale = 1f)
        {
            if (grassPrefabs == null || grassPrefabs.Length == 0) return null;

            List<GameObject> validPrefabs = new List<GameObject>();
            for (int p = 0; p < grassPrefabs.Length; p++)
            {
                if (grassPrefabs[p] != null) validPrefabs.Add(grassPrefabs[p]);
            }
            if (validPrefabs.Count == 0) return null;

            GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            if (prefab == null) return null;

            float yRot = Random.Range(0f, 360f);
            GameObject obj = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yRot, 0f));
            obj.name = $"Grass_{Random.Range(100, 999)}";

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                float maxDim = Mathf.Max(bounds.size.x, bounds.size.z);
                if (maxDim > 0.01f)
                {
                    // Target ~2.0m footprint for grass tiles/clusters with scale variation
                    float targetDim = 2.0f * scale;
                    obj.transform.localScale *= (targetDim / maxDim);
                }

                // Ground exactly at position.y
                renderers = obj.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds fb = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        fb.Encapsulate(renderers[i].bounds);
                    Vector3 p = obj.transform.position;
                    p.y += (position.y - fb.min.y);
                    obj.transform.position = p;
                }
            }

            return obj;
        }

        /// <summary>
        /// Builds grass using either prefab or procedural tuft based on availability.
        /// </summary>
        public static GameObject BuildGrass(Vector3 position, GameObject[] prefabs, Shader shader, float scale = 1f)
        {
            if (prefabs != null && prefabs.Length > 0)
                return BuildPrefab(position, prefabs, scale);
            if (shader != null)
                return BuildProceduralTuft(position, shader, scale);
            return null;
        }

        /// <summary>
        /// Scatters 3D grass clumps across all open green spaces of the city ground:
        /// checks candidate positions against buildings, roads, water, and beaches.
        /// Also populates lush riverside reed/grass banks along waterways.
        /// </summary>
        public static int ScatterGroundGreenery(
            Transform parent,
            float radius,
            List<Bounds> buildingBounds,
            List<Bounds> roadBounds,
            List<WaterAreaInfo> waterAreas,
            List<WaterwayInfo> waterways,
            List<Bounds> beachBounds,
            GameObject[] prefabs,
            Shader shader,
            float spacing = 2.8f)
        {
            if (parent == null) return 0;
            int placedCount = 0;

            float rSq = radius * radius * 1.15f;
            float surfaceY = 0.05f;

            for (float x = -radius; x <= radius; x += spacing)
            {
                for (float z = -radius; z <= radius; z += spacing)
                {
                    // Add natural jitter to break grid patterns
                    float rx = x + Random.Range(-spacing * 0.42f, spacing * 0.42f);
                    float rz = z + Random.Range(-spacing * 0.42f, spacing * 0.42f);

                    if (rx * rx + rz * rz > rSq) continue;

                    Vector3 pos = new Vector3(rx, surfaceY, rz);

                    // 1. Building collision check
                    if (buildingBounds != null)
                    {
                        bool inBuilding = false;
                        for (int b = 0; b < buildingBounds.Count; b++)
                        {
                            if (buildingBounds[b].Contains(pos))
                            {
                                inBuilding = true;
                                break;
                            }
                        }
                        if (inBuilding) continue;
                    }

                    // 2. Road collision check
                    if (roadBounds != null)
                    {
                        bool inRoad = false;
                        for (int r = 0; r < roadBounds.Count; r++)
                        {
                            if (roadBounds[r].Contains(pos))
                            {
                                inRoad = true;
                                break;
                            }
                        }
                        if (inRoad) continue;
                    }

                    // 3. Water collision check
                    if (WaterBuilder.IsPointInWater(pos, waterAreas, waterways, 0.6f))
                        continue;

                    // 4. Beach collision check
                    if (beachBounds != null)
                    {
                        bool inBeach = false;
                        for (int bc = 0; bc < beachBounds.Count; bc++)
                        {
                            if (beachBounds[bc].Contains(pos))
                            {
                                inBeach = true;
                                break;
                            }
                        }
                        if (inBeach) continue;
                    }

                    // Candidate is on open green terrain! Spawn primary grass clump
                    float scale = Random.Range(0.85f, 1.45f);
                    GameObject g = BuildGrass(pos, prefabs, shader, scale);
                    if (g != null)
                    {
                        g.transform.SetParent(parent);
                        placedCount++;
                    }

                    // Layered natural cluster: 45% chance to spawn an adjacent secondary tuft to form lush continuous carpets
                    if (Random.value < 0.45f)
                    {
                        Vector2 offset2D = Random.insideUnitCircle * (spacing * 0.40f);
                        Vector3 satPos = new Vector3(pos.x + offset2D.x, surfaceY, pos.z + offset2D.y);

                        bool satBlocked = false;
                        if (buildingBounds != null)
                        {
                            for (int b = 0; b < buildingBounds.Count; b++)
                            {
                                if (buildingBounds[b].Contains(satPos)) { satBlocked = true; break; }
                            }
                        }
                        if (!satBlocked && roadBounds != null)
                        {
                            for (int r = 0; r < roadBounds.Count; r++)
                            {
                                if (roadBounds[r].Contains(satPos)) { satBlocked = true; break; }
                            }
                        }
                        if (!satBlocked && !WaterBuilder.IsPointInWater(satPos, waterAreas, waterways, 0.5f))
                        {
                            GameObject sat = BuildGrass(satPos, prefabs, shader, Random.Range(0.65f, 1.15f));
                            if (sat != null)
                            {
                                sat.transform.SetParent(parent);
                                placedCount++;
                            }
                        }
                    }

                    // Extra riverside grass: if close to water bank (margin 0.8m to 4.5m), spawn a complementary cluster
                    if (WaterBuilder.IsPointInWater(pos, waterAreas, waterways, 4.5f) &&
                        !WaterBuilder.IsPointInWater(pos, waterAreas, waterways, 0.8f))
                    {
                        Vector3 riverPos = pos + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                        riverPos.y = surfaceY;
                        if (!WaterBuilder.IsPointInWater(riverPos, waterAreas, waterways, 0.6f))
                        {
                            GameObject rg = BuildGrass(riverPos, prefabs, shader, Random.Range(1.1f, 1.7f));
                            if (rg != null)
                            {
                                rg.transform.SetParent(parent);
                                placedCount++;
                            }
                        }
                    }
                }
            }

            return placedCount;
        }

        /// <summary>
        /// Scatters grass clumps inside any polygon (e.g. OSM park, garden, meadow, green land).
        /// </summary>
        public static List<GameObject> ScatterInPolygon(List<Vector3> polygon, int count,
            GameObject[] prefabs, Shader shader, float surfaceY = 0.05f)
        {
            List<GameObject> grassObjects = new List<GameObject>();
            if (polygon == null || polygon.Count < 3) return grassObjects;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (polygon[i].x < minX) minX = polygon[i].x;
                if (polygon[i].x > maxX) maxX = polygon[i].x;
                if (polygon[i].z < minZ) minZ = polygon[i].z;
                if (polygon[i].z > maxZ) maxZ = polygon[i].z;
            }

            int attempts = count * 3;
            int placed = 0;

            for (int a = 0; a < attempts && placed < count; a++)
            {
                float rx = Random.Range(minX, maxX);
                float rz = Random.Range(minZ, maxZ);

                if (GeometryUtils.PointInPolygon(rx, rz, polygon))
                {
                    Vector3 pos = new Vector3(rx, surfaceY, rz);
                    float scale = Random.Range(0.85f, 1.45f);
                    GameObject g = BuildGrass(pos, prefabs, shader, scale);
                    if (g != null)
                    {
                        grassObjects.Add(g);
                        placed++;
                    }
                }
            }

            return grassObjects;
        }

        /// <summary>
        /// Scatters grass clumps within a circular park or green space.
        /// </summary>
        public static List<GameObject> ScatterInCircle(Vector3 center, float radius, int count,
            GameObject[] prefabs, Shader shader, float surfaceY = 0.05f)
        {
            List<GameObject> grassObjects = new List<GameObject>();

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * radius;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, surfaceY, Mathf.Sin(angle) * dist);
                float scale = Random.Range(0.85f, 1.45f);

                GameObject g = BuildGrass(pos, prefabs, shader, scale);
                if (g != null) grassObjects.Add(g);
            }

            return grassObjects;
        }
    }
}
