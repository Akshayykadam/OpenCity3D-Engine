using UnityEngine;
using UnityEditor;
using GeoCity3D;
using GeoCity3D.Visuals;

namespace GeoCity3D.Editor
{
    public static class DemoSetup
    {
        [MenuItem("GeoCity3D/Setup Demo Scene")]
        public static void Setup()
        {
            CityController controller = Object.FindObjectOfType<CityController>();
            if (controller == null)
            {
                GameObject go = new GameObject("CityController");
                controller = go.AddComponent<CityController>();
            }

            string matPath = "Assets/GeoCity3D/Materials";
            if (!AssetDatabase.IsValidFolder(matPath))
                AssetDatabase.CreateFolder("Assets/GeoCity3D", "Materials");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            // Buildings — use asset pack facade textures if available
            Texture2D wallColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Residential Buildings Set/Materials/Wall_C.jpg");
            Texture2D wallNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Residential Buildings Set/Materials/Wall_N.jpg");
            Texture2D wallSpecular = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Residential Buildings Set/Materials/Wall_S.jpg");
            Texture2D wallAO = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Residential Buildings Set/Materials/Hotel_Hous_AO.png");

            if (wallColor != null)
            {
                controller.BuildingWallMaterial = CreateBuildingMaterial(matPath, "BuildingWallMat", shader,
                    wallColor, wallNormal, wallSpecular, wallAO);
            }
            else
            {
                controller.BuildingWallMaterial = CreateSolidMaterial(matPath, "BuildingWallMat", shader,
                    new Color(0.82f, 0.82f, 0.82f), 0.15f);
            }
            controller.BuildingRoofMaterial = CreateSolidMaterial(matPath, "BuildingRoofMat", shader,
                new Color(0.45f, 0.43f, 0.42f), 0.1f);

            // ── Road materials — use highway PBR textures if available ──
            string roadTexPath = "Assets/GeoCity3D/Textures/Road";
            Texture2D roadAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{roadTexPath}/highway-lanes_albedo.png");
            Texture2D roadNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{roadTexPath}/highway-lanes_normal-ogl.png");
            Texture2D roadAO = AssetDatabase.LoadAssetAtPath<Texture2D>($"{roadTexPath}/highway-lanes_ao.png");

            if (roadAlbedo != null)
            {
                controller.MotorwayMaterial = CreatePBRMaterial(matPath, "MotorwayMat", shader,
                    roadAlbedo, roadNormal, roadAO, 0.15f, new Vector2(1f, 4f));
                controller.PrimaryRoadMaterial = CreatePBRMaterial(matPath, "PrimaryRoadMat", shader,
                    roadAlbedo, roadNormal, roadAO, 0.15f, new Vector2(1f, 3f));
                controller.ResidentialRoadMaterial = CreatePBRMaterial(matPath, "ResidentialRoadMat", shader,
                    roadAlbedo, roadNormal, roadAO, 0.15f, new Vector2(1f, 2f));
                controller.FootpathMaterial = CreatePBRMaterial(matPath, "FootpathMat", shader,
                    roadAlbedo, roadNormal, roadAO, 0.2f, new Vector2(0.5f, 2f));
                controller.CrosswalkMaterial = CreatePBRMaterial(matPath, "CrosswalkMat", shader,
                    roadAlbedo, roadNormal, roadAO, 0.15f, new Vector2(1f, 1f));
                Debug.Log("DemoSetup: Using PBR highway textures for roads.");
            }
            else
            {
                controller.MotorwayMaterial = CreateTexturedMaterial(matPath, "MotorwayMat", shader,
                    TextureGenerator.CreateMotorwayTexture(), 0.05f);
                controller.PrimaryRoadMaterial = CreateTexturedMaterial(matPath, "PrimaryRoadMat", shader,
                    TextureGenerator.CreatePrimaryRoadTexture(), 0.05f);
                controller.ResidentialRoadMaterial = CreateTexturedMaterial(matPath, "ResidentialRoadMat", shader,
                    TextureGenerator.CreateResidentialRoadTexture(), 0.05f);
                controller.FootpathMaterial = CreateTexturedMaterial(matPath, "FootpathMat", shader,
                    TextureGenerator.CreateFootpathTexture(), 0.05f);
                controller.CrosswalkMaterial = CreateTexturedMaterial(matPath, "CrosswalkMat", shader,
                    TextureGenerator.CreateCrosswalkTexture(), 0.05f);
            }

            // General road fallback
            controller.RoadMaterial = CreateSolidMaterial(matPath, "RoadMat", shader,
                new Color(0.22f, 0.22f, 0.24f), 0.05f);
            controller.SidewalkMaterial = CreateSolidMaterial(matPath, "SidewalkMat", shader,
                new Color(0.60f, 0.60f, 0.60f), 0.1f);

            // ── Ground & Park — solid green ──
            controller.GroundMaterial = CreateSolidMaterial(matPath, "GroundMat", shader,
                new Color(0.18f, 0.40f, 0.12f), 0.1f);
            controller.ParkMaterial = CreateSolidMaterial(matPath, "ParkMat", shader,
                new Color(0.18f, 0.55f, 0.12f), 0.05f);

            controller.WaterMaterial = CreateSolidMaterial(matPath, "WaterMat", shader,
                new Color(0.15f, 0.30f, 0.38f), 0.6f);

            // Find and assign building prefabs
            System.Collections.Generic.List<GameObject> prefabs = new System.Collections.Generic.List<GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Residential Buildings Set" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                        prefabs.Add(prefab);
                }
            }
            controller.BuildingPrefabs = prefabs.ToArray();

            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = controller.gameObject;
            
            Debug.Log("Demo Scene Setup Complete! Roads use per-type textured materials. Open 'GeoCity3D > City Generator' to build a city.");
        }

        private static Material CreateSolidMaterial(string folder, string matName, Shader shader,
            Color color, float smoothness)
        {
            string matAssetPath = $"{folder}/{matName}.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }

        private static Material CreateTexturedMaterial(string folder, string matName, Shader shader,
            Texture2D texture, float smoothness)
        {
            string matAssetPath = $"{folder}/{matName}.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            // Save texture as an asset
            string texPath = $"{folder}/{matName}_Tex.asset";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) != null)
                AssetDatabase.DeleteAsset(texPath);
            AssetDatabase.CreateAsset(texture, texPath);

            Material mat = new Material(shader);
            mat.mainTexture = texture;
            mat.color = Color.white;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }

        private static Material CreatePBRMaterial(string folder, string matName, Shader shader,
            Texture2D albedo, Texture2D normalMap, Texture2D aoMap,
            float smoothness, Vector2 tiling)
        {
            string matAssetPath = $"{folder}/{matName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            Material mat = new Material(shader);
            mat.color = Color.white;

            // Albedo
            mat.mainTexture = albedo;
            mat.mainTextureScale = tiling;

            // Normal map
            if (normalMap != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1.0f);
                mat.SetTextureScale("_BumpMap", tiling);
            }

            // Ambient Occlusion
            if (aoMap != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", aoMap);
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetTextureScale("_OcclusionMap", tiling);
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }

        private static Material CreateBuildingMaterial(string folder, string matName, Shader shader,
            Texture2D colorMap, Texture2D normalMap, Texture2D specularMap, Texture2D aoMap)
        {
            string matAssetPath = $"{folder}/{matName}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            Material mat = new Material(shader);
            mat.color = Color.white;

            // Color / Albedo map
            mat.mainTexture = colorMap;
            mat.mainTextureScale = new Vector2(3f, 3f); // Tile for repetition

            // Normal map
            if (normalMap != null)
            {
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetFloat("_BumpScale", 1.0f);
                    mat.SetTextureScale("_BumpMap", new Vector2(3f, 3f));
                }
            }

            // Specular / Metallic map
            if (specularMap != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", specularMap);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.SetTextureScale("_MetallicGlossMap", new Vector2(3f, 3f));
                }
                else if (mat.HasProperty("_SpecGlossMap"))
                {
                    mat.SetTexture("_SpecGlossMap", specularMap);
                    mat.SetTextureScale("_SpecGlossMap", new Vector2(3f, 3f));
                }
            }

            // Ambient Occlusion
            if (aoMap != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", aoMap);
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetTextureScale("_OcclusionMap", new Vector2(3f, 3f));
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.15f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            Debug.Log($"Created textured building material with facade textures: {matAssetPath}");
            return mat;
        }
    }
}

