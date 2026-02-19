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

            // Force-recreate all materials
            controller.BuildingWallMaterial = ForceCreateMaterial(matPath, "BuildingWallMat", shader,
                TextureGenerator.CreateFacadeTexture(), "ProceduralFacade");
            controller.BuildingRoofMaterial = ForceCreateMaterial(matPath, "BuildingRoofMat", shader,
                TextureGenerator.CreateRoofTexture(), "ProceduralRoof");
            controller.RoadMaterial = ForceCreateMaterial(matPath, "RoadMat", shader,
                TextureGenerator.CreateRoadTexture(), "ProceduralRoad");
            controller.SidewalkMaterial = ForceCreateMaterial(matPath, "SidewalkMat", shader,
                TextureGenerator.CreateSidewalkTexture(), "ProceduralSidewalk");
            controller.GroundMaterial = ForceCreateMaterial(matPath, "GroundMat", shader,
                TextureGenerator.CreateGroundTexture(), "ProceduralGround");
            controller.ParkMaterial = ForceCreateMaterial(matPath, "ParkMat", shader,
                TextureGenerator.CreateParkTexture(), "ProceduralPark");
            controller.WaterMaterial = ForceCreateMaterial(matPath, "WaterMat", shader,
                TextureGenerator.CreateWaterTexture(), "ProceduralWater");

            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = controller.gameObject;
            
            Debug.Log("Demo Scene Setup Complete! 7 materials generated. Open 'GeoCity3D > City Generator' to build a city.");
        }

        private static Material ForceCreateMaterial(string folder, string matName, Shader shader, Texture2D texture, string texName)
        {
            string matAssetPath = $"{folder}/{matName}.mat";
            string texAssetPath = $"{folder}/{texName}.asset";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath) != null)
                AssetDatabase.DeleteAsset(texAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            AssetDatabase.CreateAsset(texture, texAssetPath);

            Material mat = new Material(shader);
            mat.mainTexture = texture;
            AssetDatabase.CreateAsset(mat, matAssetPath);

            return mat;
        }
    }
}
