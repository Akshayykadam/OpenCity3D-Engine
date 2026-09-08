using UnityEngine;
using System.Collections.Generic;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Adds automatic LOD (Level of Detail) to building GameObjects.
    /// Optimized for massive cities:
    /// - LOD0: Full architectural model (up close).
    /// - LOD1: Simplified box sharing a single unit cube mesh and an 8-color quantized palette with GPU Instancing.
    /// - LOD2: Culled at distance to preserve GPU rasterization and vertex budget.
    /// </summary>
    public static class LODBuilder
    {
        private static Mesh _sharedUnitCube;
        private static readonly Dictionary<int, Material> _sharedPalette = new Dictionary<int, Material>();

        /// <summary>
        /// Clears the cached material palette between city generation runs.
        /// </summary>
        public static void ResetPalette()
        {
            _sharedPalette.Clear();
        }

        /// <summary>
        /// Adds a LODGroup to the given building with a simplified instanced box LOD1.
        /// </summary>
        /// <param name="building">The building GameObject to add LOD to.</param>
        /// <param name="lodMaterial">Material to use for the LOD1 box (optional, auto-picks dominant color if null).</param>
        public static void AddLOD(GameObject building, Material lodMaterial = null)
        {
            if (building == null) return;

            // Get all renderers for LOD0 (the full detail model)
            Renderer[] lod0Renderers = building.GetComponentsInChildren<Renderer>();
            if (lod0Renderers.Length == 0) return;

            // Calculate combined bounds
            Bounds totalBounds = lod0Renderers[0].bounds;
            for (int i = 1; i < lod0Renderers.Length; i++)
                totalBounds.Encapsulate(lod0Renderers[i].bounds);

            // Skip tiny objects
            if (totalBounds.size.magnitude < 0.5f) return;

            // Pick dominant color from LOD0 if no material provided, mapped to shared instanced palette
            if (lodMaterial == null)
            {
                Color dominant = GetDominantColor(lod0Renderers);
                lodMaterial = GetOrCreateSharedPaletteMaterial(dominant);
            }

            // Create LOD1: an instanced box sharing the unit cube mesh scaled to building dimensions
            GameObject lod1Box = CreateBoxLOD(building, totalBounds, lodMaterial);

            // Add LODGroup component
            LODGroup lodGroup = building.AddComponent<LODGroup>();

            LOD[] lods = new LOD[3];
            // LOD0: Full detail (visible when building occupies > 12% of screen height)
            lods[0] = new LOD(0.12f, lod0Renderers);
            // LOD1: Instanced box (visible between 12% and 3% of screen height)
            lods[1] = new LOD(0.03f, lod1Box.GetComponentsInChildren<Renderer>());
            // LOD2: Culled (below 0.8% of screen height — too far to resolve individual buildings)
            lods[2] = new LOD(0.008f, new Renderer[0]);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        /// <summary>
        /// Creates a simple box mesh that matches the building's bounding box using a shared unit cube.
        /// </summary>
        private static GameObject CreateBoxLOD(GameObject parent, Bounds bounds, Material mat)
        {
            GameObject box = new GameObject("LOD1_Box");
            box.transform.SetParent(parent.transform, false);

            // Position the box at the center of the bounds in local space
            Vector3 localCenter = parent.transform.InverseTransformPoint(bounds.center);
            box.transform.localPosition = localCenter;

            // Scale the box to match building dimensions (accounting for parent scale)
            Vector3 boundsSize = bounds.size;
            Vector3 parentScale = parent.transform.lossyScale;
            Vector3 localSize = new Vector3(
                parentScale.x != 0 ? boundsSize.x / parentScale.x : boundsSize.x,
                parentScale.y != 0 ? boundsSize.y / parentScale.y : boundsSize.y,
                parentScale.z != 0 ? boundsSize.z / parentScale.z : boundsSize.z
            );
            box.transform.localScale = localSize;

            MeshFilter mf = box.AddComponent<MeshFilter>();
            MeshRenderer mr = box.AddComponent<MeshRenderer>();

            mf.sharedMesh = GetSharedUnitCube();
            mr.sharedMaterial = mat;

            // Disable shadow casting for LOD1 to save performance, keep shadow receiving
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            box.SetActive(true);
            return box;
        }

        /// <summary>
        /// Returns a single shared unit cube mesh (1x1x1 centered at origin) reused across all LOD1 boxes.
        /// This enables Unity GPU Instancing to draw thousands of buildings in a few draw calls.
        /// </summary>
        private static Mesh GetSharedUnitCube()
        {
            if (_sharedUnitCube != null) return _sharedUnitCube;

            _sharedUnitCube = new Mesh();
            _sharedUnitCube.name = "LOD_SharedUnitCube";
            float half = 0.5f;

            Vector3[] vertices = new Vector3[]
            {
                // Front face (-Z)
                new Vector3(-half, -half, -half), new Vector3( half, -half, -half),
                new Vector3( half,  half, -half), new Vector3(-half,  half, -half),
                // Back face (+Z)
                new Vector3( half, -half,  half), new Vector3(-half, -half,  half),
                new Vector3(-half,  half,  half), new Vector3( half,  half,  half),
                // Top face (+Y)
                new Vector3(-half,  half, -half), new Vector3( half,  half, -half),
                new Vector3( half,  half,  half), new Vector3(-half,  half,  half),
                // Bottom face (-Y)
                new Vector3(-half, -half,  half), new Vector3( half, -half,  half),
                new Vector3( half, -half, -half), new Vector3(-half, -half, -half),
                // Left face (-X)
                new Vector3(-half, -half,  half), new Vector3(-half, -half, -half),
                new Vector3(-half,  half, -half), new Vector3(-half,  half,  half),
                // Right face (+X)
                new Vector3( half, -half, -half), new Vector3( half, -half,  half),
                new Vector3( half,  half,  half), new Vector3( half,  half, -half),
            };

            int[] triangles = new int[]
            {
                 0,  2,  1,  0,  3,  2,   // Front
                 4,  6,  5,  4,  7,  6,   // Back
                 8, 10,  9,  8, 11, 10,   // Top
                12, 14, 13, 12, 15, 14,   // Bottom
                16, 18, 17, 16, 19, 18,   // Left
                20, 22, 21, 20, 23, 22,   // Right
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            };

            _sharedUnitCube.vertices = vertices;
            _sharedUnitCube.triangles = triangles;
            _sharedUnitCube.normals = normals;
            _sharedUnitCube.RecalculateBounds();

            return _sharedUnitCube;
        }

        /// <summary>
        /// Finds dominant color across renderers.
        /// </summary>
        private static Color GetDominantColor(Renderer[] renderers)
        {
            Color dominantColor = new Color(0.7f, 0.7f, 0.7f);
            float largestArea = 0f;

            foreach (var r in renderers)
            {
                if (r != null && r.sharedMaterial != null)
                {
                    float area = r.bounds.size.x * r.bounds.size.y +
                                 r.bounds.size.y * r.bounds.size.z +
                                 r.bounds.size.x * r.bounds.size.z;

                    if (area > largestArea)
                    {
                        largestArea = area;
                        if (r.sharedMaterial.HasProperty("_Color"))
                            dominantColor = r.sharedMaterial.color;
                        else if (r.sharedMaterial.HasProperty("_BaseColor"))
                            dominantColor = r.sharedMaterial.GetColor("_BaseColor");
                    }
                }
            }

            return dominantColor;
        }

        /// <summary>
        /// Returns a quantized palette material with GPU Instancing enabled.
        /// Quantizes RGB to 4 discrete levels, allowing thousands of distant buildings
        /// to share a handful of materials and render with GPU instancing.
        /// </summary>
        public static Material GetOrCreateSharedPaletteMaterial(Color dominantColor)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(dominantColor.r * 3f), 0, 3);
            int g = Mathf.Clamp(Mathf.RoundToInt(dominantColor.g * 3f), 0, 3);
            int b = Mathf.Clamp(Mathf.RoundToInt(dominantColor.b * 3f), 0, 3);
            int key = (r << 4) | (g << 2) | b;

            if (_sharedPalette.TryGetValue(key, out Material mat) && mat != null)
                return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader);
            mat.name = $"LOD1_SharedPalette_{key}";
            Color quantizedColor = new Color(r / 3f, g / 3f, b / 3f, 1f);
            mat.color = quantizedColor;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", quantizedColor);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.1f);
            mat.enableInstancing = true;

            _sharedPalette[key] = mat;
            return mat;
        }
    }
}
