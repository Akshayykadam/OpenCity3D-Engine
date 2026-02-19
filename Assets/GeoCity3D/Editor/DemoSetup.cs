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
            // 1. Create CityController
            CityController controller = Object.FindObjectOfType<CityController>();
            if (controller == null)
            {
                GameObject go = new GameObject("CityController");
                controller = go.AddComponent<CityController>();
            }

            // 2. Create/Load Materials
            string matPath = "Assets/GeoCity3D/Materials";
            if (!AssetDatabase.IsValidFolder(matPath))
            {
                AssetDatabase.CreateFolder("Assets/GeoCity3D", "Materials");
            }

            // Helper to find best available shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            
            // --- Facade Material ---
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>($"{matPath}/BuildingWallMat.mat");
            if (wallMat == null)
            {
                wallMat = new Material(shader);
                Texture2D wallTex = TextureGenerator.CreateFacadeTexture();
                AssetDatabase.CreateAsset(wallTex, $"{matPath}/ProceduralFacade.asset");
                wallMat.mainTexture = wallTex;
                AssetDatabase.CreateAsset(wallMat, $"{matPath}/BuildingWallMat.mat");
            }

            // --- Roof Material ---
             Material roofMat = AssetDatabase.LoadAssetAtPath<Material>($"{matPath}/BuildingRoofMat.mat");
            if (roofMat == null)
            {
                roofMat = new Material(shader);
                Texture2D roofTex = TextureGenerator.CreateRoofTexture();
                AssetDatabase.CreateAsset(roofTex, $"{matPath}/ProceduralRoof.asset");
                roofMat.mainTexture = roofTex;
                AssetDatabase.CreateAsset(roofMat, $"{matPath}/BuildingRoofMat.mat");
            }

            // --- Road Material ---
            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>($"{matPath}/RoadMat.mat");
            if (roadMat == null)
            {
                roadMat = new Material(shader);
                 Texture2D roadTex = TextureGenerator.CreateRoadTexture();
                AssetDatabase.CreateAsset(roadTex, $"{matPath}/ProceduralRoad.asset");
                roadMat.mainTexture = roadTex;
                AssetDatabase.CreateAsset(roadMat, $"{matPath}/RoadMat.mat");
            }

            // 3. Assign to Controller
            controller.BuildingWallMaterial = wallMat;
            controller.BuildingRoofMaterial = roofMat;
            controller.RoadMaterial = roadMat;

            // 4. Select Controller
            Selection.activeGameObject = controller.gameObject;
            
            Debug.Log("Demo Scene Setup Complete! Materials generated with procedural textures. Open 'GeoCity3D > City Generator' to build a city.");
        }
    }
}
