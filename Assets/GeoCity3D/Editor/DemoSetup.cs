using UnityEngine;
using UnityEditor;
using GeoCity3D;

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

            Material buildingMat = AssetDatabase.LoadAssetAtPath<Material>($"{matPath}/BuildingMat.mat");
            if (buildingMat == null)
            {
                buildingMat = new Material(shader);
                buildingMat.color = Color.gray;
                AssetDatabase.CreateAsset(buildingMat, $"{matPath}/BuildingMat.mat");
            }

            Material roadMat = AssetDatabase.LoadAssetAtPath<Material>($"{matPath}/RoadMat.mat");
            if (roadMat == null)
            {
                roadMat = new Material(shader);
                roadMat.color = Color.black;
                AssetDatabase.CreateAsset(roadMat, $"{matPath}/RoadMat.mat");
            }

            // 3. Assign to Controller
            controller.BuildingMaterial = buildingMat;
            controller.RoadMaterial = roadMat;

            // 4. Select Controller
            Selection.activeGameObject = controller.gameObject;
            
            Debug.Log("Demo Scene Setup Complete! Open 'GeoCity3D > City Generator' to build a city.");
        }
    }
}
