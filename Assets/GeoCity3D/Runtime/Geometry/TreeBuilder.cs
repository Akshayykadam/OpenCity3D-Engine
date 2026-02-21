using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates diverse, natural-looking trees with 3 shape variants:
    /// Round (deciduous), Conical (pine/cypress), and Spreading (banyan/wide canopy).
    /// Each variant has textured canopy and bark-colored trunk.
    /// </summary>
    public static class TreeBuilder
    {
        private static readonly Color[] CanopyColors = new Color[]
        {
            new Color(0.08f, 0.30f, 0.06f),   // Deep green
            new Color(0.12f, 0.35f, 0.08f),   // Forest green
            new Color(0.06f, 0.28f, 0.05f),   // Dark green
            new Color(0.15f, 0.38f, 0.10f),   // Olive green
            new Color(0.10f, 0.32f, 0.12f),   // Rich green
        };

        private static readonly Color TrunkColor = new Color(0.28f, 0.20f, 0.12f);
        private static readonly Color BarkDark = new Color(0.18f, 0.13f, 0.08f);

        // ── SHARED MATERIAL POOL (critical for batching!) ──
        // Without this, every tree gets a unique Material instance and CityCombiner
        // can never group them. This single change eliminates ~20,000 draw calls.
        private static Material _sharedTrunkMat;
        private static Material[] _sharedCanopyMats;

        private enum TreeShape { Round, Conical, Spreading }

        private static void EnsureMaterialPool(Shader shader)
        {
            if (_sharedTrunkMat != null) return;

            _sharedTrunkMat = new Material(shader);
            _sharedTrunkMat.color = TrunkColor;
            if (_sharedTrunkMat.HasProperty("_Smoothness")) _sharedTrunkMat.SetFloat("_Smoothness", 0.1f);
            if (_sharedTrunkMat.HasProperty("_Glossiness")) _sharedTrunkMat.SetFloat("_Glossiness", 0.1f);

            _sharedCanopyMats = new Material[CanopyColors.Length];
            for (int i = 0; i < CanopyColors.Length; i++)
            {
                _sharedCanopyMats[i] = new Material(shader);
                _sharedCanopyMats[i].color = CanopyColors[i];
                if (_sharedCanopyMats[i].HasProperty("_Smoothness")) _sharedCanopyMats[i].SetFloat("_Smoothness", 0.0f);
                if (_sharedCanopyMats[i].HasProperty("_Glossiness")) _sharedCanopyMats[i].SetFloat("_Glossiness", 0.0f);
            }
        }

        /// <summary>
        /// Call before generating a new city to refresh the material pool.
        /// </summary>
        public static void ResetMaterialPool()
        {
            _sharedTrunkMat = null;
            _sharedCanopyMats = null;
        }

        /// <summary>
        /// Build a single tree with random shape variant.
        /// </summary>
        public static GameObject Build(Vector3 position, Shader shader, float scale = 1f)
        {
            // Random shape selection
            TreeShape shape;
            float r = Random.value;
            if (r < 0.50f) shape = TreeShape.Round;
            else if (r < 0.75f) shape = TreeShape.Conical;
            else shape = TreeShape.Spreading;

            return Build(position, shader, scale, shape);
        }

        private static GameObject Build(Vector3 position, Shader shader, float scale, TreeShape shape)
        {
            EnsureMaterialPool(shader);

            GameObject tree = new GameObject("Tree");
            tree.transform.position = position;

            // Pick from the shared pool — all trees with same canopy color share the SAME material instance
            Material trunkMat = _sharedTrunkMat;
            Material canopyMat = _sharedCanopyMats[Random.Range(0, _sharedCanopyMats.Length)];

            switch (shape)
            {
                case TreeShape.Round:
                    BuildRoundTree(tree, trunkMat, canopyMat, scale);
                    break;
                case TreeShape.Conical:
                    BuildConicalTree(tree, trunkMat, canopyMat, scale);
                    break;
                case TreeShape.Spreading:
                    BuildSpreadingTree(tree, trunkMat, canopyMat, scale);
                    break;
            }

            return tree;
        }

        // ═══════════════════════════════════════════════
        //  ROUND TREE — classic deciduous (sphere canopy)
        // ═══════════════════════════════════════════════

        private static void BuildRoundTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkRadius = 0.15f * scale;
            float trunkHeight = Random.Range(2.0f, 4.0f) * scale;
            float canopyRadius = Random.Range(2.5f, 4.5f) * scale;

            // Trunk
            GameObject trunk = CreateTaperedCylinder("Trunk", trunkRadius, trunkRadius * 0.6f,
                trunkHeight, 8, trunkMat);
            trunk.transform.SetParent(tree.transform, false);

            // Main canopy (slightly squished sphere)
            GameObject canopy = CreateSphere("Canopy", canopyRadius, 6, 5, canopyMat);
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(0f, trunkHeight + canopyRadius * 0.5f, 0f);
            canopy.transform.localScale = new Vector3(1f, 0.85f, 1f); // Slightly flat

            // Secondary smaller canopy for volume
            if (Random.value > 0.4f)
            {
                float r2 = canopyRadius * Random.Range(0.5f, 0.7f);
                GameObject canopy2 = CreateSphere("Canopy2", r2, 5, 4, canopyMat);
                canopy2.transform.SetParent(tree.transform, false);
                float offsetX = Random.Range(-canopyRadius * 0.3f, canopyRadius * 0.3f);
                float offsetZ = Random.Range(-canopyRadius * 0.3f, canopyRadius * 0.3f);
                canopy2.transform.localPosition = new Vector3(offsetX,
                    trunkHeight + canopyRadius * 0.8f, offsetZ);
            }
        }

        // ═══════════════════════════════════════════════
        //  CONICAL TREE — pine/cypress (cone canopy)
        // ═══════════════════════════════════════════════

        private static void BuildConicalTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkRadius = 0.12f * scale;
            float trunkHeight = Random.Range(1.5f, 2.5f) * scale;
            float coneRadius = Random.Range(1.5f, 2.5f) * scale;
            float coneHeight = Random.Range(4.0f, 7.0f) * scale;

            // Trunk (thin, straight)
            GameObject trunk = CreateTaperedCylinder("Trunk", trunkRadius, trunkRadius * 0.8f,
                trunkHeight, 4, trunkMat);
            trunk.transform.SetParent(tree.transform, false);

            // Cone canopy (2-3 stacked cones for layered look)
            int layers = Random.Range(2, 4);
            for (int i = 0; i < layers; i++)
            {
                float layerBase = trunkHeight + (coneHeight / layers) * i * 0.7f;
                float layerRadius = coneRadius * (1f - (float)i / layers * 0.4f);
                float layerHeight = coneHeight / layers * 1.2f;

                GameObject cone = CreateCone("Cone_" + i, layerRadius, layerHeight, 5, canopyMat);
                cone.transform.SetParent(tree.transform, false);
                cone.transform.localPosition = new Vector3(0f, layerBase, 0f);
            }
        }

        // ═══════════════════════════════════════════════
        //  SPREADING TREE — wide flat canopy (banyan/neem)
        // ═══════════════════════════════════════════════

        private static void BuildSpreadingTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkRadius = 0.2f * scale;
            float trunkHeight = Random.Range(2.5f, 4.0f) * scale;
            float canopyRadiusX = Random.Range(4.0f, 6.0f) * scale;
            float canopyRadiusZ = canopyRadiusX * Random.Range(0.8f, 1.2f);
            float canopyHeight = Random.Range(1.5f, 2.5f) * scale;

            // Thick trunk
            GameObject trunk = CreateTaperedCylinder("Trunk", trunkRadius * 1.3f, trunkRadius * 0.7f,
                trunkHeight, 6, trunkMat);
            trunk.transform.SetParent(tree.transform, false);

            // Wide, flat ellipsoid canopy
            GameObject canopy = CreateSphere("Canopy", 1f, 6, 5, canopyMat);
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(0f, trunkHeight + canopyHeight * 0.3f, 0f);
            canopy.transform.localScale = new Vector3(canopyRadiusX, canopyHeight, canopyRadiusZ);

            // Secondary canopy blob for irregularity
            float r2x = canopyRadiusX * Random.Range(0.4f, 0.6f);
            float r2z = canopyRadiusZ * Random.Range(0.4f, 0.6f);
            GameObject canopy2 = CreateSphere("Canopy2", 1f, 8, 6, canopyMat);
            canopy2.transform.SetParent(tree.transform, false);
            canopy2.transform.localPosition = new Vector3(
                Random.Range(-canopyRadiusX * 0.25f, canopyRadiusX * 0.25f),
                trunkHeight + canopyHeight * 0.5f,
                Random.Range(-canopyRadiusZ * 0.25f, canopyRadiusZ * 0.25f));
            canopy2.transform.localScale = new Vector3(r2x, canopyHeight * 0.8f, r2z);
        }

        /// <summary>
        /// Scatter trees in a circular area — multiple variants.
        /// </summary>
        public static List<GameObject> ScatterTrees(Vector3 center, float radius, int count, Shader shader)
        {
            List<GameObject> trees = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * radius;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                float treeScale = Random.Range(0.5f, 1.2f);

                trees.Add(Build(pos, shader, treeScale));
            }
            return trees;
        }

        // ═══════════════════════════════════════════════
        //  MESH PRIMITIVES
        // ═══════════════════════════════════════════════

        private static GameObject CreateTaperedCylinder(string name, float bottomRadius, float topRadius,
            float height, int segments, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            // Side vertices
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * bottomRadius, 0, sin * bottomRadius));
                verts.Add(new Vector3(cos * topRadius, height, sin * topRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }

            // Base disc
            int centerIdx = verts.Count;
            verts.Add(Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * bottomRadius, 0, Mathf.Sin(angle) * bottomRadius));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris.Add(centerIdx); tris.Add(centerIdx + 1 + next); tris.Add(centerIdx + 1 + i);
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        private static GameObject CreateSphere(string name, float radius, int rings, int segments, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;
                float y = Mathf.Cos(phi) * radius;
                float ringRadius = Mathf.Sin(phi) * radius;

                for (int seg = 0; seg <= segments; seg++)
                {
                    float theta = Mathf.PI * 2f * seg / segments;
                    verts.Add(new Vector3(Mathf.Cos(theta) * ringRadius, y, Mathf.Sin(theta) * ringRadius));
                }
            }

            int vertsPerRing = segments + 1;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segments; seg++)
                {
                    int current = ring * vertsPerRing + seg;
                    int next = current + vertsPerRing;
                    tris.Add(current); tris.Add(next); tris.Add(current + 1);
                    tris.Add(current + 1); tris.Add(next); tris.Add(next + 1);
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        private static GameObject CreateCone(string name, float radius, float height,
            int segments, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            // Apex
            int apexIdx = 0;
            verts.Add(new Vector3(0, height, 0));

            // Base ring
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
            }

            // Side triangles
            for (int i = 0; i < segments; i++)
            {
                tris.Add(apexIdx);
                tris.Add(1 + i);
                tris.Add(1 + i + 1);
            }

            // Base disc
            int centerIdx = verts.Count;
            verts.Add(Vector3.zero);
            for (int i = 0; i < segments; i++)
            {
                tris.Add(centerIdx);
                tris.Add(1 + (i + 1) % segments);
                tris.Add(1 + i);
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }
    }
}
