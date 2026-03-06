using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Adds automatic LOD (Level of Detail) to building GameObjects.
    /// LOD0: Full model, LOD1: Simple box mesh, LOD2: Culled.
    /// </summary>
    public static class LODBuilder
    {
        /// <summary>
        /// Adds a LODGroup to the given building with a simplified box LOD1.
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

            // Auto-pick dominant color from LOD0 if no material provided
            if (lodMaterial == null)
            {
                lodMaterial = CreateLODMaterial(lod0Renderers);
            }

            // Create LOD1: a simple box that matches the building bounds
            GameObject lod1Box = CreateBoxLOD(building, totalBounds, lodMaterial);

            // Add LODGroup component
            LODGroup lodGroup = building.AddComponent<LODGroup>();

            LOD[] lods = new LOD[3];
            // LOD0: Full detail (visible when building occupies > 5% of screen)
            lods[0] = new LOD(0.05f, lod0Renderers);
            // LOD1: Simple box (visible between 5% and 1% of screen)
            lods[1] = new LOD(0.01f, lod1Box.GetComponentsInChildren<Renderer>());
            // LOD2: Culled (below 1% of screen — too far to see)
            lods[2] = new LOD(0f, new Renderer[0]);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        /// <summary>
        /// Creates a simple box mesh that matches the building's bounding box.
        /// </summary>
        private static GameObject CreateBoxLOD(GameObject parent, Bounds bounds, Material mat)
        {
            GameObject box = new GameObject("LOD1_Box");
            box.transform.SetParent(parent.transform, false);

            // Position the box at the center of the bounds in local space
            Vector3 localCenter = parent.transform.InverseTransformPoint(bounds.center);
            box.transform.localPosition = localCenter;

            // Create box mesh matching the bounds size
            MeshFilter mf = box.AddComponent<MeshFilter>();
            MeshRenderer mr = box.AddComponent<MeshRenderer>();

            // Scale the box to match building dimensions
            Vector3 boundsSize = bounds.size;
            // Transform bounds size to local space (account for parent scale)
            Vector3 parentScale = parent.transform.lossyScale;
            Vector3 localSize = new Vector3(
                parentScale.x != 0 ? boundsSize.x / parentScale.x : boundsSize.x,
                parentScale.y != 0 ? boundsSize.y / parentScale.y : boundsSize.y,
                parentScale.z != 0 ? boundsSize.z / parentScale.z : boundsSize.z
            );

            mf.sharedMesh = CreateBoxMesh(localSize);
            mr.sharedMaterial = mat;

            // Disable shadow casting for LOD1 to save performance
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Start disabled — LODGroup will enable it when needed
            box.SetActive(true);

            return box;
        }

        /// <summary>
        /// Creates a box mesh with the given dimensions centered at origin.
        /// </summary>
        private static Mesh CreateBoxMesh(Vector3 size)
        {
            Mesh mesh = new Mesh();
            mesh.name = "LOD_Box";

            float w = size.x * 0.5f;
            float h = size.y * 0.5f;
            float d = size.z * 0.5f;

            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-w, -h, -d), new Vector3( w, -h, -d),
                new Vector3( w,  h, -d), new Vector3(-w,  h, -d),
                // Back face
                new Vector3( w, -h,  d), new Vector3(-w, -h,  d),
                new Vector3(-w,  h,  d), new Vector3( w,  h,  d),
                // Top face
                new Vector3(-w,  h, -d), new Vector3( w,  h, -d),
                new Vector3( w,  h,  d), new Vector3(-w,  h,  d),
                // Bottom face
                new Vector3(-w, -h,  d), new Vector3( w, -h,  d),
                new Vector3( w, -h, -d), new Vector3(-w, -h, -d),
                // Left face
                new Vector3(-w, -h,  d), new Vector3(-w, -h, -d),
                new Vector3(-w,  h, -d), new Vector3(-w,  h,  d),
                // Right face
                new Vector3( w, -h, -d), new Vector3( w, -h,  d),
                new Vector3( w,  h,  d), new Vector3( w,  h, -d),
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

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;

            return mesh;
        }

        /// <summary>
        /// Creates a simple material that matches the dominant color of the building.
        /// </summary>
        private static Material CreateLODMaterial(Renderer[] renderers)
        {
            // Find the most common color across all renderers
            Color dominantColor = Color.gray;
            float largestArea = 0f;

            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null)
                {
                    // Estimate "importance" by renderer bounds area
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
                        else
                            dominantColor = Color.gray;
                    }
                }
            }

            // Create a simple unlit-like material for performance
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.name = "LOD_Auto";
            mat.color = dominantColor;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", dominantColor);
            // Low smoothness for matte look matching low-poly aesthetic
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.1f);

            return mat;
        }
    }
}
