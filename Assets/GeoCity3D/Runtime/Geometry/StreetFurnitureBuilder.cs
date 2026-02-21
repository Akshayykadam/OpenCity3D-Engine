using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates procedural street furniture — street lights, bollards, etc.
    /// </summary>
    public static class StreetFurnitureBuilder
    {
        private static readonly Color PoleColor = new Color(0.25f, 0.25f, 0.27f);   // Dark metal gray
        private static readonly Color LampColor = new Color(0.95f, 0.90f, 0.65f);   // Warm lamp glow

        // ── SHARED MATERIAL POOL (critical for batching!) ──
        private static Material _sharedPoleMat;
        private static Material _sharedLampMat;

        private static void EnsureMaterialPool(Shader shader)
        {
            if (_sharedPoleMat != null) return;

            _sharedPoleMat = new Material(shader);
            _sharedPoleMat.color = PoleColor;
            if (_sharedPoleMat.HasProperty("_Smoothness")) _sharedPoleMat.SetFloat("_Smoothness", 0.5f);
            if (_sharedPoleMat.HasProperty("_Glossiness")) _sharedPoleMat.SetFloat("_Glossiness", 0.5f);

            _sharedLampMat = new Material(shader);
            _sharedLampMat.color = LampColor;
            if (_sharedLampMat.HasProperty("_Smoothness")) _sharedLampMat.SetFloat("_Smoothness", 0.8f);
            if (_sharedLampMat.HasProperty("_Glossiness")) _sharedLampMat.SetFloat("_Glossiness", 0.8f);
            if (_sharedLampMat.HasProperty("_EmissionColor"))
            {
                _sharedLampMat.EnableKeyword("_EMISSION");
                _sharedLampMat.SetColor("_EmissionColor", LampColor * 0.5f);
            }
        }

        /// <summary>
        /// Call before generating a new city to refresh the material pool.
        /// </summary>
        public static void ResetMaterialPool()
        {
            _sharedPoleMat = null;
            _sharedLampMat = null;
        }

        /// <summary>
        /// Place street lights along a road path at regular intervals, alternating sides.
        /// </summary>
        public static List<GameObject> PlaceStreetLights(List<Vector3> roadPath, Shader shader,
            float spacing = 25f)
        {
            EnsureMaterialPool(shader);

            List<GameObject> lights = new List<GameObject>();
            if (roadPath == null || roadPath.Count < 2) return lights;

            float accumulated = 0f;
            bool rightSide = true;

            for (int i = 0; i < roadPath.Count - 1; i++)
            {
                Vector3 a = roadPath[i];
                Vector3 b = roadPath[i + 1];
                float segLen = Vector3.Distance(a, b);
                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                float pos = spacing - accumulated;
                while (pos < segLen)
                {
                    Vector3 point = Vector3.Lerp(a, b, pos / segLen);
                    float offset = rightSide ? 5.5f : -5.5f;
                    Vector3 lightPos = point + right * offset;
                    lightPos.y = 0f;

                    GameObject light = BuildStreetLight(lightPos, dir);
                    lights.Add(light);

                    rightSide = !rightSide;
                    pos += spacing;
                }

                accumulated = segLen - (pos - spacing);
            }

            return lights;
        }

        /// <summary>
        /// Build a single street light: pole + arm + lamp housing.
        /// </summary>
        private static GameObject BuildStreetLight(Vector3 position, Vector3 roadDir)
        {
            GameObject root = new GameObject("StreetLight");
            root.transform.position = position;

            Material poleMat = _sharedPoleMat;
            Material lampMat = _sharedLampMat;

            // ── Pole (vertical cylinder) ──
            float poleHeight = Random.Range(5.5f, 7f);
            float poleRadius = 0.08f;
            GameObject pole = CreateCylinder("Pole", poleRadius, poleHeight, 4, poleMat);
            pole.transform.SetParent(root.transform, false);

            // ── Arm (angled outward toward road) ──
            float armLength = 1.2f;
            float armRadius = 0.05f;
            GameObject arm = CreateCylinder("Arm", armRadius, armLength, 3, poleMat);
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0, poleHeight, 0);
            // Angle the arm 30° down toward the road
            arm.transform.localRotation = Quaternion.Euler(0, 0, -60f);

            // ── Lamp housing (small box at end of arm) ──
            GameObject lamp = CreateBox("Lamp", new Vector3(0.3f, 0.1f, 0.3f), lampMat);
            lamp.transform.SetParent(root.transform, false);
            // Position at end of arm
            float armAngleRad = -60f * Mathf.Deg2Rad;
            lamp.transform.localPosition = new Vector3(
                Mathf.Sin(-armAngleRad) * armLength,
                poleHeight + Mathf.Cos(-armAngleRad) * armLength - 0.05f,
                0f);

            return root;
        }

        // ── Simple cylinder mesh ──
        private static GameObject CreateCylinder(string name, float radius, float height,
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

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                verts.Add(new Vector3(cos * radius, 0, sin * radius));
                verts.Add(new Vector3(cos * radius, height, sin * radius));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }

        // ── Simple box mesh ──
        private static GameObject CreateBox(string name, Vector3 size, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;

            Vector3[] verts = new Vector3[]
            {
                // Bottom face
                new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz),
                new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz),
                // Top face
                new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz),
                new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz),
            };

            int[] tris = new int[]
            {
                // Bottom
                0, 2, 1, 0, 3, 2,
                // Top
                4, 5, 6, 4, 6, 7,
                // Front
                0, 1, 5, 0, 5, 4,
                // Back
                2, 3, 7, 2, 7, 6,
                // Left
                0, 4, 7, 0, 7, 3,
                // Right
                1, 2, 6, 1, 6, 5,
            };

            Mesh mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
        }
    }
}
