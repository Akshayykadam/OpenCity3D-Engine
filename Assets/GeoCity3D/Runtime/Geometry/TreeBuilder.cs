using System;
using System.Collections.Generic;
using System.Globalization;
using GeoCity3D.Data;
using GeoCity3D.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates botanically realistic procedural 3D trees with natural wood anatomy:
    /// - Root flares and fluting at ground level.
    /// - Organic curved trunks with visible primary scaffold branches.
    /// - Compound multi-cluster foliage clouds with 3D noise displacement.
    /// - Spherical botanical normals for lush, soft volume shading.
    /// - Procedural bark and foliage textures with normal maps.
    /// - Full shadow casting and receiving.
    /// 
    /// Supports 4 species archetypes:
    /// Round (Oak/Maple broadleaf), Conical (Pine/Spruce/Fir),
    /// Spreading (Acacia/Banyan/Park umbrella), and Columnar (Birch/Poplar/Boulevard).
    /// </summary>
    public static class TreeBuilder
    {
        private static readonly Color[] CanopyColors = new Color[]
        {
            new Color(0.13f, 0.38f, 0.10f),   // Lush Forest Green
            new Color(0.18f, 0.44f, 0.12f),   // Summer Green
            new Color(0.22f, 0.42f, 0.14f),   // Olive Green
            new Color(0.09f, 0.30f, 0.08f),   // Deep Evergreen / Pine
            new Color(0.16f, 0.48f, 0.16f),   // Vibrant Spring Green
            new Color(0.26f, 0.43f, 0.14f),   // Warm Golden-Green
            new Color(0.11f, 0.34f, 0.12f),   // Rich Elm Green
        };

        private static readonly Color TrunkColor = new Color(0.32f, 0.24f, 0.16f);

        // ── SHARED MATERIAL POOL (critical for batching in CityCombiner) ──
        private static Material _sharedTrunkMat;
        private static Material[] _sharedCanopyMats;

        // Cached procedural textures
        private static Texture2D _cachedBarkTex;
        private static Texture2D _cachedBarkNorm;
        private static Texture2D _cachedFoliageNorm;
        private static Texture2D[] _cachedFoliageTexs;

        public enum TreeShape { Round, Conical, Spreading, Columnar }

        private struct Branch
        {
            public Vector3 start;
            public Vector3 end;
            public float radiusStart;
            public float radiusEnd;
        }

        private struct FoliageCluster
        {
            public Vector3 center;
            public Vector3 scale;
            public float radius;
            public float seed;
        }

        private static void EnsureMaterialPool(Shader shader)
        {
            if (_sharedTrunkMat != null) return;

            // Generate bark textures
            if (_cachedBarkTex == null) _cachedBarkTex = TextureGenerator.CreateBarkTexture(256, 512);
            if (_cachedBarkNorm == null) _cachedBarkNorm = TextureGenerator.CreateBarkNormalMap(256, 512);

            _sharedTrunkMat = new Material(shader);
            _sharedTrunkMat.name = "Tree_Trunk_Shared";
            _sharedTrunkMat.color = TrunkColor;
            if (_sharedTrunkMat.HasProperty("_BaseColor")) _sharedTrunkMat.SetColor("_BaseColor", TrunkColor);
            if (_sharedTrunkMat.HasProperty("_MainTex") && _cachedBarkTex != null) _sharedTrunkMat.SetTexture("_MainTex", _cachedBarkTex);
            if (_sharedTrunkMat.HasProperty("_BaseMap") && _cachedBarkTex != null) _sharedTrunkMat.SetTexture("_BaseMap", _cachedBarkTex);
            if (_sharedTrunkMat.HasProperty("_BumpMap") && _cachedBarkNorm != null)
            {
                _sharedTrunkMat.SetTexture("_BumpMap", _cachedBarkNorm);
                _sharedTrunkMat.EnableKeyword("_NORMALMAP");
                _sharedTrunkMat.SetFloat("_BumpScale", 1.2f);
            }
            if (_sharedTrunkMat.HasProperty("_Smoothness")) _sharedTrunkMat.SetFloat("_Smoothness", 0.15f);
            if (_sharedTrunkMat.HasProperty("_Glossiness")) _sharedTrunkMat.SetFloat("_Glossiness", 0.15f);
            _sharedTrunkMat.enableInstancing = true;

            // Generate foliage normal map (shared across all foliage shades)
            if (_cachedFoliageNorm == null) _cachedFoliageNorm = TextureGenerator.CreateFoliageNormalMap(256, 256);

            _sharedCanopyMats = new Material[CanopyColors.Length];
            if (_cachedFoliageTexs == null || _cachedFoliageTexs.Length != CanopyColors.Length)
                _cachedFoliageTexs = new Texture2D[CanopyColors.Length];

            for (int i = 0; i < CanopyColors.Length; i++)
            {
                if (_cachedFoliageTexs[i] == null)
                    _cachedFoliageTexs[i] = TextureGenerator.CreateFoliageTexture(256, 256, CanopyColors[i]);

                _sharedCanopyMats[i] = new Material(shader);
                _sharedCanopyMats[i].name = $"Tree_Canopy_{i}";
                _sharedCanopyMats[i].color = CanopyColors[i];
                if (_sharedCanopyMats[i].HasProperty("_BaseColor")) _sharedCanopyMats[i].SetColor("_BaseColor", CanopyColors[i]);
                if (_sharedCanopyMats[i].HasProperty("_MainTex") && _cachedFoliageTexs[i] != null) _sharedCanopyMats[i].SetTexture("_MainTex", _cachedFoliageTexs[i]);
                if (_sharedCanopyMats[i].HasProperty("_BaseMap") && _cachedFoliageTexs[i] != null) _sharedCanopyMats[i].SetTexture("_BaseMap", _cachedFoliageTexs[i]);
                if (_sharedCanopyMats[i].HasProperty("_BumpMap") && _cachedFoliageNorm != null)
                {
                    _sharedCanopyMats[i].SetTexture("_BumpMap", _cachedFoliageNorm);
                    _sharedCanopyMats[i].EnableKeyword("_NORMALMAP");
                    _sharedCanopyMats[i].SetFloat("_BumpScale", 1.0f);
                }
                if (_sharedCanopyMats[i].HasProperty("_Smoothness")) _sharedCanopyMats[i].SetFloat("_Smoothness", 0.05f);
                if (_sharedCanopyMats[i].HasProperty("_Glossiness")) _sharedCanopyMats[i].SetFloat("_Glossiness", 0.05f);
                _sharedCanopyMats[i].enableInstancing = true;
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
            float r = Random.value;
            TreeShape shape;
            if (r < 0.45f) shape = TreeShape.Round;
            else if (r < 0.70f) shape = TreeShape.Conical;
            else if (r < 0.88f) shape = TreeShape.Columnar;
            else shape = TreeShape.Spreading;

            return Build(position, shader, scale, shape);
        }

        /// <summary>
        /// Builds a procedural tree with a specified shape variant.
        /// </summary>
        public static GameObject Build(Vector3 position, Shader shader, float scale, TreeShape shape)
        {
            EnsureMaterialPool(shader);

            GameObject tree = new GameObject("Tree");
            tree.transform.position = position;

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
                case TreeShape.Columnar:
                    BuildColumnarTree(tree, trunkMat, canopyMat, scale);
                    break;
                case TreeShape.Spreading:
                    BuildSpreadingTree(tree, trunkMat, canopyMat, scale);
                    break;
            }

            return tree;
        }

        /// <summary>
        /// Builds a procedural tree based on real OpenStreetMap node tags.
        /// Interprets leaf_type, species, height, and diameter_crown.
        /// </summary>
        public static GameObject BuildFromOsm(Vector3 position, OsmNode node, Shader shader)
        {
            float scale = 1.0f;

            if (node.HasTag("height"))
            {
                string rawH = node.GetTag("height").Trim().ToLower().Replace("m", "").Trim();
                if (float.TryParse(rawH, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedH))
                {
                    scale = Mathf.Clamp(parsedH / 6.0f, 0.5f, 3.0f);
                }
            }
            else if (node.HasTag("diameter_crown") || node.HasTag("crown_diameter"))
            {
                string rawD = (node.GetTag("diameter_crown") ?? node.GetTag("crown_diameter")).Trim().ToLower().Replace("m", "").Trim();
                if (float.TryParse(rawD, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedD))
                {
                    scale = Mathf.Clamp(parsedD / 5.0f, 0.5f, 3.0f);
                }
            }
            else
            {
                scale = Random.Range(0.75f, 1.35f);
            }

            TreeShape shape = TreeShape.Round;
            string leafType = (node.GetTag("leaf_type") ?? "").ToLower();
            string genus = (node.GetTag("genus") ?? node.GetTag("species") ?? "").ToLower();

            if (leafType == "needleleaved" || leafType == "conifer" ||
                genus.Contains("pinus") || genus.Contains("picea") || genus.Contains("abies") ||
                genus.Contains("cedrus") || genus.Contains("larix") || genus.Contains("taxus"))
            {
                shape = TreeShape.Conical;
            }
            else if (genus.Contains("cupressus") || genus.Contains("populus") || genus.Contains("betula") ||
                     genus.Contains("fastigiata") || genus.Contains("columnar"))
            {
                shape = TreeShape.Columnar;
            }
            else if (genus.Contains("palm") || genus.Contains("ficus") || genus.Contains("banyan") ||
                     genus.Contains("platanus") || genus.Contains("acacia") || genus.Contains("albizia"))
            {
                shape = TreeShape.Spreading;
            }
            else if (leafType == "broadleaved" || genus.Contains("quercus") || genus.Contains("acer") || genus.Contains("ulmus"))
            {
                shape = TreeShape.Round;
            }
            else
            {
                float r = Random.value;
                if (r < 0.45f) shape = TreeShape.Round;
                else if (r < 0.70f) shape = TreeShape.Conical;
                else if (r < 0.88f) shape = TreeShape.Columnar;
                else shape = TreeShape.Spreading;
            }

            return Build(position, shader, scale, shape);
        }

        // ═══════════════════════════════════════════════
        //  1. ROUND TREE — Classic Deciduous / Oak
        // ═══════════════════════════════════════════════

        private static void BuildRoundTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkHeight = Random.Range(3.4f, 4.6f) * scale;
            float baseRadius = Random.Range(0.24f, 0.32f) * scale;
            float topRadius = baseRadius * 0.65f;
            float rootFlare = 1.65f;

            Vector2 trunkCurve = new Vector2(
                Random.Range(-0.35f, 0.35f) * scale,
                Random.Range(-0.35f, 0.35f) * scale);

            // Scaffold branches
            List<Branch> branches = new List<Branch>();
            int branchCount = Random.Range(3, 5);
            float forkH = trunkHeight * Random.Range(0.68f, 0.82f);
            float baseAngle = Random.Range(0f, Mathf.PI * 2f);

            List<FoliageCluster> clusters = new List<FoliageCluster>();

            for (int i = 0; i < branchCount; i++)
            {
                float angle = baseAngle + (float)i / branchCount * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float branchLen = Random.Range(2.2f, 3.2f) * scale;
                float branchElevation = Random.Range(1.2f, 2.2f) * scale;

                Vector3 start = new Vector3(
                    trunkCurve.x * 0.7f + Mathf.Cos(angle) * (baseRadius * 0.7f),
                    forkH + Random.Range(-0.2f, 0.2f),
                    trunkCurve.y * 0.7f + Mathf.Sin(angle) * (baseRadius * 0.7f));

                Vector3 end = start + new Vector3(
                    Mathf.Cos(angle) * branchLen,
                    branchElevation,
                    Mathf.Sin(angle) * branchLen);

                branches.Add(new Branch
                {
                    start = start,
                    end = end,
                    radiusStart = baseRadius * 0.45f,
                    radiusEnd = baseRadius * 0.20f
                });

                // Foliage cluster on branch tip
                clusters.Add(new FoliageCluster
                {
                    center = end + new Vector3(0f, Random.Range(0.2f, 0.5f) * scale, 0f),
                    radius = Random.Range(1.8f, 2.4f) * scale,
                    scale = new Vector3(1.15f, 0.95f, 1.15f),
                    seed = Random.Range(10f, 900f)
                });

                // Filler sub-cluster along branch mid-point
                if (Random.value > 0.3f)
                {
                    Vector3 mid = Vector3.Lerp(start, end, 0.55f) + new Vector3(
                        Random.Range(-0.4f, 0.4f),
                        Random.Range(0.3f, 0.7f),
                        Random.Range(-0.4f, 0.4f)) * scale;

                    clusters.Add(new FoliageCluster
                    {
                        center = mid,
                        radius = Random.Range(1.2f, 1.7f) * scale,
                        scale = Vector3.one,
                        seed = Random.Range(10f, 900f)
                    });
                }
            }

            // Central apex crown cluster
            Vector3 crownCenter = new Vector3(trunkCurve.x, trunkHeight + 1.8f * scale, trunkCurve.y);
            clusters.Add(new FoliageCluster
            {
                center = crownCenter,
                radius = Random.Range(2.4f, 3.2f) * scale,
                scale = new Vector3(1.1f, 1.0f, 1.1f),
                seed = Random.Range(10f, 900f)
            });

            // Top crown dome
            clusters.Add(new FoliageCluster
            {
                center = crownCenter + new Vector3(0f, 1.2f * scale, 0f),
                radius = Random.Range(1.8f, 2.3f) * scale,
                scale = new Vector3(0.9f, 0.85f, 0.9f),
                seed = Random.Range(10f, 900f)
            });

            // Assemble GameObjects
            CreateMeshObject("Trunk", tree.transform, BuildTrunkMesh(baseRadius, topRadius, trunkHeight, rootFlare, trunkCurve, branches), trunkMat);
            CreateMeshObject("Canopy", tree.transform, BuildCanopyMesh(clusters, crownCenter), canopyMat);
        }

        // ═══════════════════════════════════════════════
        //  2. CONICAL TREE — Pine / Spruce / Fir
        // ═══════════════════════════════════════════════

        private static void BuildConicalTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkHeight = Random.Range(9.0f, 13.5f) * scale;
            float baseRadius = Random.Range(0.20f, 0.28f) * scale;
            float topRadius = 0.05f * scale;
            float rootFlare = 1.55f;

            Vector2 trunkCurve = new Vector2(
                Random.Range(-0.15f, 0.15f) * scale,
                Random.Range(-0.15f, 0.15f) * scale);

            List<Branch> branches = new List<Branch>();
            List<FoliageCluster> clusters = new List<FoliageCluster>();

            int tiers = Random.Range(5, 8);
            float startY = Random.Range(1.8f, 2.4f) * scale;
            float topY = trunkHeight * 0.90f;

            for (int t = 0; t < tiers; t++)
            {
                float frac = (float)t / (tiers - 1);
                float tierY = Mathf.Lerp(startY, topY, frac);
                float tierSpread = Mathf.Lerp(3.2f, 0.8f, frac) * scale;
                int pads = Mathf.RoundToInt(Mathf.Lerp(5f, 3f, frac));
                float rotOffset = t * 1.35f;

                for (int p = 0; p < pads; p++)
                {
                    float angle = rotOffset + (float)p / pads * Mathf.PI * 2f;
                    Vector3 trunkPos = new Vector3(
                        Mathf.Lerp(0f, trunkCurve.x, tierY / trunkHeight),
                        tierY,
                        Mathf.Lerp(0f, trunkCurve.y, tierY / trunkHeight));

                    Vector3 padDir = new Vector3(Mathf.Cos(angle), -0.15f, Mathf.Sin(angle)).normalized;
                    Vector3 padPos = trunkPos + padDir * tierSpread;

                    // Branch stub supporting bough
                    branches.Add(new Branch
                    {
                        start = trunkPos,
                        end = padPos,
                        radiusStart = baseRadius * Mathf.Lerp(0.35f, 0.12f, frac),
                        radiusEnd = baseRadius * 0.06f
                    });

                    // Drooping fir bough pad
                    clusters.Add(new FoliageCluster
                    {
                        center = padPos,
                        radius = Mathf.Lerp(1.2f, 0.6f, frac) * scale,
                        scale = new Vector3(1.25f, 0.55f, 1.25f),
                        seed = t * 100f + p * 33f
                    });
                }
            }

            // Spire top apex
            Vector3 spirePos = new Vector3(trunkCurve.x, trunkHeight, trunkCurve.y);
            clusters.Add(new FoliageCluster
            {
                center = spirePos,
                radius = 0.8f * scale,
                scale = new Vector3(0.7f, 1.6f, 0.7f),
                seed = 777f
            });

            CreateMeshObject("Trunk", tree.transform, BuildTrunkMesh(baseRadius, topRadius, trunkHeight, rootFlare, trunkCurve, branches), trunkMat);
            CreateMeshObject("Canopy", tree.transform, BuildCanopyMesh(clusters, spirePos - new Vector3(0f, trunkHeight * 0.35f, 0f)), canopyMat);
        }

        // ═══════════════════════════════════════════════
        //  3. COLUMNAR TREE — Birch / Boulevard / Cypress
        // ═══════════════════════════════════════════════

        private static void BuildColumnarTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkHeight = Random.Range(5.5f, 8.0f) * scale;
            float baseRadius = Random.Range(0.16f, 0.22f) * scale;
            float topRadius = baseRadius * 0.5f;
            float rootFlare = 1.4f;

            Vector2 trunkCurve = new Vector2(
                Random.Range(-0.1f, 0.1f) * scale,
                Random.Range(-0.1f, 0.1f) * scale);

            List<Branch> branches = new List<Branch>();
            List<FoliageCluster> clusters = new List<FoliageCluster>();

            // Upright ascending branches
            int branchCount = Random.Range(4, 6);
            float startY = trunkHeight * 0.45f;

            for (int i = 0; i < branchCount; i++)
            {
                float frac = (float)i / branchCount;
                float angle = frac * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float by = Mathf.Lerp(startY, trunkHeight * 0.85f, frac);

                Vector3 bStart = new Vector3(
                    Mathf.Lerp(0f, trunkCurve.x, by / trunkHeight),
                    by,
                    Mathf.Lerp(0f, trunkCurve.y, by / trunkHeight));

                Vector3 bEnd = bStart + new Vector3(
                    Mathf.Cos(angle) * Random.Range(0.8f, 1.3f) * scale,
                    Random.Range(1.5f, 2.6f) * scale,
                    Mathf.Sin(angle) * Random.Range(0.8f, 1.3f) * scale);

                branches.Add(new Branch
                {
                    start = bStart,
                    end = bEnd,
                    radiusStart = baseRadius * 0.35f,
                    radiusEnd = baseRadius * 0.12f
                });

                clusters.Add(new FoliageCluster
                {
                    center = bEnd,
                    radius = Random.Range(1.1f, 1.5f) * scale,
                    scale = new Vector3(0.85f, 1.5f, 0.85f),
                    seed = i * 67f
                });
            }

            // Central vertical spine clusters
            for (int c = 0; c < 4; c++)
            {
                float cy = Mathf.Lerp(trunkHeight * 0.50f, trunkHeight + 1.8f * scale, c / 3f);
                clusters.Add(new FoliageCluster
                {
                    center = new Vector3(trunkCurve.x * (cy / trunkHeight), cy, trunkCurve.y * (cy / trunkHeight)),
                    radius = Random.Range(1.3f, 1.7f) * scale,
                    scale = new Vector3(0.9f, 1.35f, 0.9f),
                    seed = c * 153f
                });
            }

            Vector3 crownCenter = new Vector3(trunkCurve.x, trunkHeight * 0.8f, trunkCurve.y);
            CreateMeshObject("Trunk", tree.transform, BuildTrunkMesh(baseRadius, topRadius, trunkHeight, rootFlare, trunkCurve, branches), trunkMat);
            CreateMeshObject("Canopy", tree.transform, BuildCanopyMesh(clusters, crownCenter), canopyMat);
        }

        // ═══════════════════════════════════════════════
        //  4. SPREADING TREE — Acacia / Banyan / Rain Tree
        // ═══════════════════════════════════════════════

        private static void BuildSpreadingTree(GameObject tree, Material trunkMat, Material canopyMat, float scale)
        {
            float trunkHeight = Random.Range(3.2f, 4.5f) * scale;
            float baseRadius = Random.Range(0.36f, 0.48f) * scale;
            float topRadius = baseRadius * 0.65f;
            float rootFlare = 1.85f; // Heavy buttress roots

            Vector2 trunkCurve = new Vector2(
                Random.Range(-0.25f, 0.25f) * scale,
                Random.Range(-0.25f, 0.25f) * scale);

            List<Branch> branches = new List<Branch>();
            List<FoliageCluster> clusters = new List<FoliageCluster>();

            int limbCount = Random.Range(4, 6);
            float baseAngle = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < limbCount; i++)
            {
                float angle = baseAngle + (float)i / limbCount * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                float limbLen = Random.Range(3.5f, 5.2f) * scale;
                float limbRise = Random.Range(1.2f, 2.2f) * scale;

                Vector3 start = new Vector3(
                    trunkCurve.x * 0.6f + Mathf.Cos(angle) * (baseRadius * 0.6f),
                    trunkHeight * Random.Range(0.65f, 0.85f),
                    trunkCurve.y * 0.6f + Mathf.Sin(angle) * (baseRadius * 0.6f));

                Vector3 limbEnd = start + new Vector3(
                    Mathf.Cos(angle) * limbLen,
                    limbRise,
                    Mathf.Sin(angle) * limbLen);

                branches.Add(new Branch
                {
                    start = start,
                    end = limbEnd,
                    radiusStart = baseRadius * 0.50f,
                    radiusEnd = baseRadius * 0.22f
                });

                // Secondary sub-branch branching off the primary limb
                float subAngle = angle + (Random.value > 0.5f ? 0.6f : -0.6f);
                Vector3 subStart = Vector3.Lerp(start, limbEnd, 0.6f);
                Vector3 subEnd = subStart + new Vector3(
                    Mathf.Cos(subAngle) * Random.Range(1.8f, 2.8f) * scale,
                    Random.Range(0.6f, 1.2f) * scale,
                    Mathf.Sin(subAngle) * Random.Range(1.8f, 2.8f) * scale);

                branches.Add(new Branch
                {
                    start = subStart,
                    end = subEnd,
                    radiusStart = baseRadius * 0.25f,
                    radiusEnd = baseRadius * 0.12f
                });

                // Foliage pads on limb ends (layered horizontal umbrella clouds)
                clusters.Add(new FoliageCluster
                {
                    center = limbEnd + new Vector3(0f, 0.3f * scale, 0f),
                    radius = Random.Range(2.0f, 2.8f) * scale,
                    scale = new Vector3(1.35f, 0.65f, 1.35f),
                    seed = i * 123f
                });

                clusters.Add(new FoliageCluster
                {
                    center = subEnd + new Vector3(0f, 0.2f * scale, 0f),
                    radius = Random.Range(1.5f, 2.2f) * scale,
                    scale = new Vector3(1.25f, 0.60f, 1.25f),
                    seed = i * 234f
                });
            }

            // Central umbrella canopy layer
            Vector3 crownCenter = new Vector3(trunkCurve.x, trunkHeight + 1.6f * scale, trunkCurve.y);
            clusters.Add(new FoliageCluster
            {
                center = crownCenter,
                radius = Random.Range(2.8f, 3.8f) * scale,
                scale = new Vector3(1.4f, 0.65f, 1.4f),
                seed = 999f
            });

            CreateMeshObject("Trunk", tree.transform, BuildTrunkMesh(baseRadius, topRadius, trunkHeight, rootFlare, trunkCurve, branches), trunkMat);
            CreateMeshObject("Canopy", tree.transform, BuildCanopyMesh(clusters, crownCenter), canopyMat);
        }

        // ═══════════════════════════════════════════════
        //  MESH BUILDERS
        // ═══════════════════════════════════════════════

        private static Mesh BuildTrunkMesh(
            float baseRadius,
            float topRadius,
            float trunkHeight,
            float rootFlare,
            Vector2 trunkCurve,
            List<Branch> branches)
        {
            Mesh mesh = new Mesh();
            mesh.name = "TreeTrunk";

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            int rings = 7;
            int segments = 8;

            // 1. Trunk rings with root flare and fluting
            for (int r = 0; r < rings; r++)
            {
                float t = (float)r / (rings - 1);
                float y = t * trunkHeight;

                float rad;
                if (r == 0)
                    rad = baseRadius * rootFlare;
                else if (r == 1)
                    rad = baseRadius * (1.0f + (rootFlare - 1.0f) * 0.25f);
                else
                    rad = Mathf.Lerp(baseRadius, topRadius, (t - 0.2f) / 0.8f);

                float curveFactor = t * t;
                float cx = trunkCurve.x * curveFactor;
                float cz = trunkCurve.y * curveFactor;

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float flute = (r == 0) ? (1.0f + 0.12f * Mathf.Cos(angle * 5f)) : 1.0f;

                    float vx = cx + Mathf.Cos(angle) * rad * flute;
                    float vz = cz + Mathf.Sin(angle) * rad * flute;

                    verts.Add(new Vector3(vx, y, vz));
                    uvs.Add(new Vector2((float)s / segments, y * 0.5f));
                }
            }

            int vertsPerRing = segments + 1;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int current = r * vertsPerRing + s;
                    int next = current + vertsPerRing;

                    tris.Add(current);
                    tris.Add(next);
                    tris.Add(current + 1);

                    tris.Add(current + 1);
                    tris.Add(next);
                    tris.Add(next + 1);
                }
            }

            // Bottom base cap
            int bottomCenterIdx = verts.Count;
            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s < segments; s++)
            {
                tris.Add(bottomCenterIdx);
                tris.Add(s + 1);
                tris.Add(s);
            }

            // 2. Scaffold branches
            if (branches != null)
            {
                int bSegments = 6;
                int bRings = 4;

                foreach (var b in branches)
                {
                    Vector3 axis = b.end - b.start;
                    float len = axis.magnitude;
                    if (len < 0.01f) continue;
                    Vector3 dir = axis / len;

                    Vector3 upRef = Vector3.up;
                    if (Mathf.Abs(Vector3.Dot(dir, upRef)) > 0.88f) upRef = Vector3.forward;
                    Vector3 right = Vector3.Cross(dir, upRef).normalized;
                    Vector3 up = Vector3.Cross(right, dir).normalized;

                    int branchStartIdx = verts.Count;

                    for (int r = 0; r < bRings; r++)
                    {
                        float t = (float)r / (bRings - 1);
                        Vector3 center = b.start + dir * (len * t);
                        float rad = Mathf.Lerp(b.radiusStart, b.radiusEnd, t);

                        for (int s = 0; s <= bSegments; s++)
                        {
                            float angle = (float)s / bSegments * Mathf.PI * 2f;
                            Vector3 p = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * rad;

                            verts.Add(p);
                            uvs.Add(new Vector2((float)s / bSegments, (trunkHeight + len * t) * 0.5f));
                        }
                    }

                    int bVertsPerRing = bSegments + 1;
                    for (int r = 0; r < bRings - 1; r++)
                    {
                        for (int s = 0; s < bSegments; s++)
                        {
                            int cur = branchStartIdx + r * bVertsPerRing + s;
                            int nxt = cur + bVertsPerRing;

                            tris.Add(cur);
                            tris.Add(nxt);
                            tris.Add(cur + 1);

                            tris.Add(cur + 1);
                            tris.Add(nxt);
                            tris.Add(nxt + 1);
                        }
                    }

                    // Branch tip cap
                    int tipCenterIdx = verts.Count;
                    verts.Add(b.end);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    int lastRingStart = branchStartIdx + (bRings - 1) * bVertsPerRing;
                    for (int s = 0; s < bSegments; s++)
                    {
                        tris.Add(tipCenterIdx);
                        tris.Add(lastRingStart + s);
                        tris.Add(lastRingStart + s + 1);
                    }
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCanopyMesh(List<FoliageCluster> clusters, Vector3 treeCrownCenter)
        {
            Mesh mesh = new Mesh();
            mesh.name = "TreeCanopy";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            int latRings = 7;
            int lonSegments = 10;

            foreach (var cluster in clusters)
            {
                int clusterStartIdx = verts.Count;

                for (int lat = 0; lat <= latRings; lat++)
                {
                    float phi = (float)lat / latRings * Mathf.PI;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    for (int lon = 0; lon <= lonSegments; lon++)
                    {
                        float theta = (float)lon / lonSegments * Mathf.PI * 2f;
                        float cosTheta = Mathf.Cos(theta);
                        float sinTheta = Mathf.Sin(theta);

                        Vector3 dir = new Vector3(sinPhi * cosTheta, cosPhi, sinPhi * sinTheta);

                        // Multi-octave 3D Perlin noise leaf displacement
                        float n1 = Mathf.PerlinNoise(dir.x * 2.6f + cluster.seed, dir.y * 2.6f + cluster.seed * 1.3f);
                        float n2 = Mathf.PerlinNoise(dir.z * 4.2f + cluster.seed * 2.1f, dir.x * 4.2f + cluster.seed * 0.7f);
                        float disp = 1.0f + (n1 - 0.5f) * 0.36f + (n2 - 0.5f) * 0.18f;

                        Vector3 p = cluster.center + Vector3.Scale(dir * (cluster.radius * disp), cluster.scale);

                        // Spherical botanical normal blending (soft voluminous shading)
                        Vector3 crownDir = (p - treeCrownCenter).normalized;
                        Vector3 blendedNormal = Vector3.Lerp(dir, crownDir, 0.42f).normalized;

                        verts.Add(p);
                        normals.Add(blendedNormal);
                        uvs.Add(new Vector2((float)lon / lonSegments, (float)lat / latRings));
                    }
                }

                int vertsPerLat = lonSegments + 1;
                for (int lat = 0; lat < latRings; lat++)
                {
                    for (int lon = 0; lon < lonSegments; lon++)
                    {
                        int current = clusterStartIdx + lat * vertsPerLat + lon;
                        int next = current + vertsPerLat;

                        tris.Add(current);
                        tris.Add(next);
                        tris.Add(current + 1);

                        tris.Add(current + 1);
                        tris.Add(next);
                        tris.Add(next + 1);
                    }
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            return go;
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
                float treeScale = Random.Range(0.75f, 1.25f);

                trees.Add(Build(pos, shader, treeScale));
            }
            return trees;
        }

        // ═══════════════════════════════════════════════
        //  PREFAB-BASED TREE PLACEMENT (fallback / optional)
        // ═══════════════════════════════════════════════

        public static GameObject BuildPrefab(Vector3 position, GameObject[] prefabs, float scale = 1f)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            List<GameObject> validPrefabs = new List<GameObject>();
            for (int p = 0; p < prefabs.Length; p++)
            {
                if (prefabs[p] != null) validPrefabs.Add(prefabs[p]);
            }
            if (validPrefabs.Count == 0) return null;

            GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            if (prefab == null) return null;

            float yAngle = Random.Range(0f, 360f);
            GameObject tree = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yAngle, 0f));
            tree.name = "Tree_Prefab";

            float targetHeight = Random.Range(3f, 8f) * scale;
            Renderer[] renderers = tree.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                float currentHeight = bounds.size.y;
                if (currentHeight > 0.01f)
                {
                    float s = targetHeight / currentHeight;
                    tree.transform.localScale *= s;
                }

                renderers = tree.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds fb = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        fb.Encapsulate(renderers[i].bounds);
                    Vector3 pos = tree.transform.position;
                    pos.y -= fb.min.y;
                    tree.transform.position = pos;
                }
            }

            return tree;
        }

        public static List<GameObject> ScatterTreesPrefab(Vector3 center, float radius, int count,
            GameObject[] treePrefabs)
        {
            List<GameObject> trees = new List<GameObject>();
            if (treePrefabs == null || treePrefabs.Length == 0) return trees;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * radius;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                float treeScale = Random.Range(0.6f, 1.2f);

                GameObject tree = BuildPrefab(pos, treePrefabs, treeScale);
                if (tree != null) trees.Add(tree);
            }
            return trees;
        }

        public static List<GameObject> ScatterParkNature(Vector3 center, float radius, int totalCount,
            GameObject[] treePrefabs, GameObject[] bushPrefabs, GameObject[] rockPrefabs)
        {
            List<GameObject> objects = new List<GameObject>();

            for (int i = 0; i < totalCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * radius;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);

                float r = Random.value;
                GameObject obj = null;

                if (r < 0.60f && treePrefabs != null && treePrefabs.Length > 0)
                {
                    obj = BuildPrefab(pos, treePrefabs, Random.Range(0.6f, 1.2f));
                }
                else if (r < 0.85f && bushPrefabs != null && bushPrefabs.Length > 0)
                {
                    obj = BuildPrefab(pos, bushPrefabs, Random.Range(0.6f, 1.0f));
                }
                else if (rockPrefabs != null && rockPrefabs.Length > 0)
                {
                    obj = BuildPrefab(pos, rockPrefabs, Random.Range(0.5f, 1.5f));
                }
                else if (treePrefabs != null && treePrefabs.Length > 0)
                {
                    obj = BuildPrefab(pos, treePrefabs, Random.Range(0.6f, 1.2f));
                }

                if (obj != null) objects.Add(obj);
            }
            return objects;
        }
    }
}
