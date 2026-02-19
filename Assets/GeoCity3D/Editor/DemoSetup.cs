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

            // Clean architectural maquette palette
            controller.BuildingWallMaterial = CreateSolidMaterial(matPath, "BuildingWallMat", shader,
                new Color(0.82f, 0.82f, 0.82f), 0.15f);
            controller.BuildingRoofMaterial = CreateSolidMaterial(matPath, "BuildingRoofMat", shader,
                new Color(0.72f, 0.72f, 0.72f), 0.1f);
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
            
            Debug.Log("Demo Scene Setup Complete! Architectural maquette style. Open 'GeoCity3D > City Generator' to build a city.");
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
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided
            mat.renderQueue = 2000;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }
    }
}
