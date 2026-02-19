using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates procedural tree meshes — tropical style with large canopies.
    /// </summary>
    public static class TreeBuilder
    {
        private static readonly Color[] TrunkColors = new Color[]
        {
            new Color(0.30f, 0.22f, 0.12f),
            new Color(0.35f, 0.25f, 0.14f),
            new Color(0.28f, 0.20f, 0.10f),
        };

        // Lush tropical greens
        private static readonly Color[] LeafColors = new Color[]
        {
            new Color(0.10f, 0.32f, 0.08f),
            new Color(0.14f, 0.38f, 0.10f),
            new Color(0.18f, 0.42f, 0.12f),
            new Color(0.12f, 0.36f, 0.06f),
            new Color(0.08f, 0.30f, 0.05f),
            new Color(0.16f, 0.40f, 0.14f),
        };

        /// <summary>
        /// Build a single tree at the given position. Default size is large (tropical).
        /// </summary>
        public static GameObject Build(Vector3 position, Material treeMat, float scale = 1f)
        {
            GameObject tree = new GameObject("Tree");
            tree.transform.position = position;

            float trunkRadius = 0.2f * scale;
            float trunkHeight = Random.Range(3.0f, 5.5f) * scale;
            int trunkSegments = 6;

            // Large tropical canopy
            float canopyRadius = Random.Range(3.0f, 5.0f) * scale;
            float canopyHeight = Random.Range(3.0f, 5.0f) * scale;
            int canopyRings = 6;
            int canopySegments = 8;

            Color trunkColor = TrunkColors[Random.Range(0, TrunkColors.Length)];
            Color leafColor = LeafColors[Random.Range(0, LeafColors.Length)];

            // ── Trunk ──
            GameObject trunk = CreateCylinder("Trunk", trunkRadius, trunkHeight, trunkSegments, trunkColor, treeMat);
            trunk.transform.SetParent(tree.transform, false);

            // ── Canopy ──
            GameObject canopy = CreateEllipsoid("Canopy", canopyRadius, canopyHeight, canopyRings, canopySegments, leafColor, treeMat);
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(
                Random.Range(-0.4f, 0.4f) * scale,
                trunkHeight + canopyHeight * 0.2f,
                Random.Range(-0.4f, 0.4f) * scale);

            // Some trees get a second canopy layer for fullness
            if (Random.value > 0.4f)
            {
                Color leaf2 = LeafColors[Random.Range(0, LeafColors.Length)];
                float r2 = canopyRadius * Random.Range(0.6f, 0.85f);
                float h2 = canopyHeight * Random.Range(0.5f, 0.7f);
                GameObject canopy2 = CreateEllipsoid("Canopy2", r2, h2, 5, 7, leaf2, treeMat);
                canopy2.transform.SetParent(tree.transform, false);
                canopy2.transform.localPosition = new Vector3(
                    Random.Range(-1f, 1f) * scale,
                    trunkHeight + canopyHeight * Random.Range(0.1f, 0.4f),
                    Random.Range(-1f, 1f) * scale);
            }

            return tree;
        }

        /// <summary>
        /// Scatter trees in a circular area.
        /// </summary>
        public static List<GameObject> ScatterTrees(Vector3 center, float radius, int count, Material treeMat)
        {
            List<GameObject> trees = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * radius;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                float scale = Random.Range(0.7f, 1.5f);

                GameObject tree = Build(pos, treeMat, scale);
                trees.Add(tree);
            }
            return trees;
        }

        private static GameObject CreateCylinder(string name, float radius, float height, int segments, Color color, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                float topRadius = radius * 0.65f;
                float xt = Mathf.Cos(angle) * topRadius;
                float zt = Mathf.Sin(angle) * topRadius;

                verts.Add(new Vector3(x, 0, z));
                Color c = color + new Color((Random.value - 0.5f) * 0.04f, (Random.value - 0.5f) * 0.02f, 0);
                c.a = 1f;
                colors.Add(c);

                verts.Add(new Vector3(xt, height, zt));
                colors.Add(c * 0.9f);
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }

            mesh.vertices = verts.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            return go;
        }

        private static GameObject CreateEllipsoid(string name, float radiusXZ, float radiusY,
            int rings, int segments, Color color, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;
                float y = Mathf.Cos(phi) * radiusY;
                float ringRadius = Mathf.Sin(phi) * radiusXZ;

                for (int seg = 0; seg <= segments; seg++)
                {
                    float theta = Mathf.PI * 2f * seg / segments;

                    // Organic wobble
                    float vWobble = 1f + Mathf.PerlinNoise(ring * 2f + seg * 1.3f + 30f, ring * 0.7f) * 0.25f - 0.125f;
                    float r = ringRadius * vWobble;

                    float x = Mathf.Cos(theta) * r;
                    float z = Mathf.Sin(theta) * r;

                    verts.Add(new Vector3(x, y, z));

                    // Deep color variation for realistic foliage
                    float shade = Mathf.PerlinNoise(seg * 1.1f + ring * 0.8f + 50f, ring * 1.5f + 70f);
                    Color c = Color.Lerp(color * 0.6f, color * 1.3f, shade);
                    // Darker underside
                    float ao = Mathf.Clamp01((y / radiusY + 1f) * 0.5f);
                    c *= Mathf.Lerp(0.5f, 1f, ao);
                    c.a = 1f;
                    colors.Add(c);
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
            mesh.colors = colors.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            return go;
        }
    }
}
