using System.Collections.Generic;
using System.Globalization;
using GeoCity3D.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates diverse, natural-looking low-poly procedural rocks and boulders directly from code.
    /// Supports multiple shape variants (Boulder, Crag/Outcrop, and Flat Stone) with shared materials
    /// for seamless CityCombiner batching.
    /// </summary>
    public static class RockBuilder
    {
        public enum RockShape { Boulder, Crag, FlatStone }

        private static readonly Color[] RockColors = new Color[]
        {
            new Color(0.48f, 0.47f, 0.45f), // Granite gray
            new Color(0.34f, 0.33f, 0.35f), // Dark slate
            new Color(0.56f, 0.51f, 0.43f), // Warm sandstone
            new Color(0.42f, 0.46f, 0.38f), // Mossy stone
            new Color(0.52f, 0.50f, 0.48f)  // River pebble gray
        };

        // Shared material pool for batching with CityCombiner
        private static Material[] _sharedRockMats;

        private static void EnsureMaterialPool(Shader shader)
        {
            if (_sharedRockMats != null && _sharedRockMats.Length > 0) return;

            _sharedRockMats = new Material[RockColors.Length];
            for (int i = 0; i < RockColors.Length; i++)
            {
                _sharedRockMats[i] = new Material(shader);
                _sharedRockMats[i].name = $"RockMat_{i}";
                _sharedRockMats[i].color = RockColors[i];
                if (_sharedRockMats[i].HasProperty("_Smoothness")) _sharedRockMats[i].SetFloat("_Smoothness", 0.05f);
                if (_sharedRockMats[i].HasProperty("_Glossiness")) _sharedRockMats[i].SetFloat("_Glossiness", 0.05f);
            }
        }

        /// <summary>
        /// Call before generating a new city to refresh the material pool.
        /// </summary>
        public static void ResetMaterialPool()
        {
            _sharedRockMats = null;
        }

        /// <summary>
        /// Builds a procedural rock at the specified position with random shape and scale.
        /// </summary>
        public static GameObject Build(Vector3 position, Shader shader, float scale = 1f)
        {
            float r = Random.value;
            RockShape shape = r < 0.55f ? RockShape.Boulder : (r < 0.80f ? RockShape.FlatStone : RockShape.Crag);
            return Build(position, shader, scale, shape);
        }

        /// <summary>
        /// Builds a procedural rock with an explicit shape variant.
        /// </summary>
        public static GameObject Build(Vector3 position, Shader shader, float scale, RockShape shape)
        {
            EnsureMaterialPool(shader);

            GameObject rock = new GameObject("ProceduralRock");
            rock.transform.position = position;
            rock.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));

            Vector3 scaleVec;
            switch (shape)
            {
                case RockShape.Crag:
                    scaleVec = new Vector3(scale * Random.Range(0.7f, 1.1f), scale * Random.Range(1.2f, 1.8f), scale * Random.Range(0.7f, 1.1f));
                    break;
                case RockShape.FlatStone:
                    scaleVec = new Vector3(scale * Random.Range(1.1f, 1.6f), scale * Random.Range(0.35f, 0.65f), scale * Random.Range(1.1f, 1.6f));
                    break;
                case RockShape.Boulder:
                default:
                    scaleVec = new Vector3(scale * Random.Range(0.85f, 1.25f), scale * Random.Range(0.7f, 1.1f), scale * Random.Range(0.85f, 1.25f));
                    break;
            }
            rock.transform.localScale = scaleVec;

            MeshFilter mf = rock.AddComponent<MeshFilter>();
            MeshRenderer mr = rock.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedRockMats[Random.Range(0, _sharedRockMats.Length)];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            mf.sharedMesh = CreateRockMesh(shape);
            return rock;
        }

        /// <summary>
        /// Builds a procedural rock based on real OpenStreetMap node tags.
        /// </summary>
        public static GameObject BuildFromOsm(Vector3 position, OsmNode node, Shader shader)
        {
            float scale = 1.2f;

            if (node.HasTag("height"))
            {
                string rawH = node.GetTag("height").Trim().ToLower().Replace("m", "").Trim();
                if (float.TryParse(rawH, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedH))
                {
                    scale = Mathf.Clamp(parsedH, 0.4f, 6.0f);
                }
            }
            else
            {
                string natural = (node.GetTag("natural") ?? "").ToLower();
                if (natural == "stone") scale = Random.Range(0.5f, 1.0f);
                else if (natural == "rock") scale = Random.Range(1.0f, 2.0f);
                else if (natural == "bare_rock") scale = Random.Range(1.8f, 3.2f);
            }

            RockShape shape = RockShape.Boulder;
            if (node.HasTag("geological") || (node.GetTag("natural") ?? "").ToLower() == "bare_rock")
            {
                shape = RockShape.Crag;
            }
            else if ((node.GetTag("natural") ?? "").ToLower() == "stone")
            {
                shape = RockShape.FlatStone;
            }

            return Build(position, shader, scale, shape);
        }

        /// <summary>
        /// Generates a flat-shaded low-poly rock mesh with randomized vertex displacement.
        /// </summary>
        private static Mesh CreateRockMesh(RockShape shape)
        {
            // Base icosahedron
            float t = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
            Vector3[] baseVerts = new Vector3[]
            {
                new Vector3(-1, t, 0).normalized,
                new Vector3(1, t, 0).normalized,
                new Vector3(-1, -t, 0).normalized,
                new Vector3(1, -t, 0).normalized,
                new Vector3(0, -1, t).normalized,
                new Vector3(0, 1, t).normalized,
                new Vector3(0, -1, -t).normalized,
                new Vector3(0, 1, -t).normalized,
                new Vector3(t, 0, -1).normalized,
                new Vector3(t, 0, 1).normalized,
                new Vector3(-t, 0, -1).normalized,
                new Vector3(-t, 0, 1).normalized
            };

            int[] indices = new int[]
            {
                0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
            };

            // Displace vertices randomly per mesh instance for organic variation
            for (int i = 0; i < baseVerts.Length; i++)
            {
                float disp = Random.Range(0.75f, 1.25f);
                baseVerts[i] *= disp;

                if (shape == RockShape.Crag)
                {
                    if (baseVerts[i].y > 0) baseVerts[i].y *= Random.Range(1.1f, 1.4f);
                }
                else if (shape == RockShape.FlatStone)
                {
                    baseVerts[i].y *= 0.55f;
                }

                // Flatten underside so rock seats naturally on the ground
                if (baseVerts[i].y < -0.2f) baseVerts[i].y *= 0.5f;
            }

            // Duplicate vertices per triangle for crisp faceted flat-shading
            Vector3[] verts = new Vector3[indices.Length];
            int[] tris = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                verts[i] = baseVerts[indices[i]];
                tris[i] = i;
            }

            Mesh mesh = new Mesh();
            mesh.name = $"ProceduralRock_{shape}";
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
