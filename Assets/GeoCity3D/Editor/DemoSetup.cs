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

            // Buildings
            controller.BuildingWallMaterial = CreateSolidMaterial(matPath, "BuildingWallMat", shader,
                new Color(0.82f, 0.82f, 0.82f), 0.15f);
            controller.BuildingRoofMaterial = CreateSolidMaterial(matPath, "BuildingRoofMat", shader,
                new Color(0.72f, 0.72f, 0.72f), 0.1f);

            // Road type materials (textured)
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

            // General road fallback
            controller.RoadMaterial = CreateSolidMaterial(matPath, "RoadMat", shader,
                new Color(0.22f, 0.22f, 0.24f), 0.05f);
            controller.SidewalkMaterial = CreateSolidMaterial(matPath, "SidewalkMat", shader,
                new Color(0.60f, 0.60f, 0.60f), 0.1f);
            controller.GroundMaterial = CreateSolidMaterial(matPath, "GroundMat", shader,
                new Color(0.35f, 0.35f, 0.37f), 0.1f);
            controller.ParkMaterial = CreateSolidMaterial(matPath, "ParkMat", shader,
                new Color(0.18f, 0.55f, 0.12f), 0.05f);
            controller.WaterMaterial = CreateSolidMaterial(matPath, "WaterMat", shader,
                new Color(0.15f, 0.30f, 0.38f), 0.6f);

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

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }
    }
}

