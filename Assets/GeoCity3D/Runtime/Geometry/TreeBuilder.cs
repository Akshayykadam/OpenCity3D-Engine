using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates solid architectural-style trees.
    /// Dark-green sphere canopies on grounded trunks with base disc.
    /// Clean maquette style with volumetric geometry.
    /// </summary>
    public static class TreeBuilder
    {
        private static readonly Color[] CanopyColors = new Color[]
        {
            new Color(0.08f, 0.28f, 0.06f),
            new Color(0.10f, 0.32f, 0.08f),
            new Color(0.06f, 0.25f, 0.05f),
            new Color(0.12f, 0.30f, 0.07f),
        };

        private static readonly Color TrunkColor = new Color(0.25f, 0.20f, 0.12f);

        /// <summary>
        /// Build a single tree — solid sphere canopy on a grounded trunk.
        /// Trunk connects firmly to ground with a base disc.
        /// </summary>
        public static GameObject Build(Vector3 position, Shader shader, float scale = 1f)
        {
            GameObject tree = new GameObject("Tree");
            tree.transform.position = position;

            float trunkRadius = 0.15f * scale;
            float trunkHeight = Random.Range(1.5f, 3.5f) * scale;
            float canopyRadius = Random.Range(2.0f, 4.0f) * scale;

            Color canopyColor = CanopyColors[Random.Range(0, CanopyColors.Length)];

            Material trunkMat = new Material(shader);
            trunkMat.color = TrunkColor;

            Material canopyMat = new Material(shader);
            canopyMat.color = canopyColor;

            // ── Trunk (tapered cylinder with base disc) ──
            GameObject trunk = CreateGroundedCylinder("Trunk", trunkRadius, trunkHeight, 8, trunkMat);
            trunk.transform.SetParent(tree.transform, false);

            // ── Canopy (smooth solid sphere) ──
            GameObject canopy = CreateSphere("Canopy", canopyRadius, 12, 10, canopyMat);
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(0f, trunkHeight + canopyRadius * 0.55f, 0f);

            return tree;
        }

        /// <summary>
        /// Scatter trees in a circular area.
        /// </summary>
        public static List<GameObject> ScatterTrees(Vector3 center, float radius, int count, Shader shader)
        {
            List<GameObject> trees = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * radius;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                float treeScale = Random.Range(0.6f, 1.3f);

                trees.Add(Build(pos, shader, treeScale));
            }
            return trees;
        }

        // ── Grounded cylinder with closed base disc ──
        private static GameObject CreateGroundedCylinder(string name, float radius, float height,
            int segments, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            float topRadius = radius * 0.65f; // Slight taper

            // Side vertices
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * radius, 0, sin * radius));
                verts.Add(new Vector3(cos * topRadius, height, sin * topRadius));
            }

            // Side triangles
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }

            // ── Base disc (ground contact) ──
            int centerIdx = verts.Count;
            verts.Add(new Vector3(0, 0, 0)); // Center of base

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                // Winding: center, next, current (face downward)
                tris.Add(centerIdx);
                tris.Add(centerIdx + 1 + next);
                tris.Add(centerIdx + 1 + i);
            }

            // ── Top disc ──
            int topCenterIdx = verts.Count;
            verts.Add(new Vector3(0, height, 0));

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * topRadius, height, Mathf.Sin(angle) * topRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris.Add(topCenterIdx);
                tris.Add(topCenterIdx + 1 + i);
                tris.Add(topCenterIdx + 1 + next);
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        // ── Smooth solid sphere ──
        private static GameObject CreateSphere(string name, float radius, int rings, int segments, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

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
                    float x = Mathf.Cos(theta) * ringRadius;
                    float z = Mathf.Sin(theta) * ringRadius;
                    verts.Add(new Vector3(x, y, z));
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
    }
}
