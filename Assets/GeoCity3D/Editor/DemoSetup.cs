using UnityEngine;
using UnityEditor;
using GeoCity3D;
using GeoCity3D.Visuals;
using System.Collections.Generic;
using System.Linq;

namespace GeoCity3D.Editor
{
    public static class DemoSetup
    {
        // SimplePoly City asset root (must be imported into this project)
        private const string SimplePolyRoot = "Assets/SimplePoly City - Low Poly Assets";
        private const string PrefabRoot = SimplePolyRoot + "/Prefab";

        [MenuItem("GeoCity3D/Setup Demo Scene")]
        public static void Setup()
        {
            CityController controller = Object.FindObjectOfType<CityController>();
            if (controller == null)
            {
                GameObject go = new GameObject("CityController");
                controller = go.AddComponent<CityController>();
            }

            // ── Materials folder ──
            string matPath = "Assets/GeoCity3D/Materials";
            if (!AssetDatabase.IsValidFolder(matPath))
                AssetDatabase.CreateFolder("Assets/GeoCity3D", "Materials");

            Shader shader = FindBestShader();

            // ── Check SimplePoly City is present ──
            bool hasSimplePoly = AssetDatabase.IsValidFolder(PrefabRoot);
            if (!hasSimplePoly)
            {
                Debug.LogWarning("SimplePoly City - Low Poly Assets not found! " +
                    "Please import the package into Assets/. Falling back to procedural mode.");
            }

            // ═══════════════════════════════════════════════════════════
            //  BUILDING PREFABS
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                // Use procedural buildings (OSM footprint geometry with solid colors)
                // SimplePoly City prefabs are used for trees, props, vehicles, etc. only
                controller.BuildingPrefabs = new GameObject[0];
                controller.BuildingGenerationMode = BuildingMode.Procedural;
                Debug.Log("DemoSetup: Using procedural buildings + SimplePoly City props/trees/vehicles.");
            }
            else
            {
                controller.BuildingPrefabs = new GameObject[0];
                controller.BuildingGenerationMode = BuildingMode.Procedural;
            }

            // ═══════════════════════════════════════════════════════════
            //  TREE PREFABS
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.TreePrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Natures",
                    new[] { "Tree" });
                Debug.Log($"DemoSetup: Loaded {controller.TreePrefabs.Length} tree prefabs.");
            }

            // ═══════════════════════════════════════════════════════════
            //  BUSH & ROCK PREFABS (parks)
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.BushPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Natures",
                    new[] { "Bush", "Pot Bush" });
                controller.RockPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Natures",
                    new[] { "Rock" });
                Debug.Log($"DemoSetup: Loaded {controller.BushPrefabs.Length} bush + {controller.RockPrefabs.Length} rock prefabs.");
            }

            // ═══════════════════════════════════════════════════════════
            //  STREET LIGHT PREFABS
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.StreetLightPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Props",
                    new[] { "Street Light" });
                Debug.Log($"DemoSetup: Loaded {controller.StreetLightPrefabs.Length} street light prefabs.");
            }

            // ═══════════════════════════════════════════════════════════
            //  TRAFFIC SIGNAL PREFABS
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.TrafficSignalPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Props",
                    new[] { "Traffic Signal", "Traffic Sign" });
                Debug.Log($"DemoSetup: Loaded {controller.TrafficSignalPrefabs.Length} traffic signal/sign prefabs.");
            }

            // ═══════════════════════════════════════════════════════════
            //  GENERAL STREET PROPS (benches, hydrants, dustbins, etc.)
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.StreetPropPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Props",
                    new[] { "Bench", "Hydrant", "Dustbin", "Bus Stop", "Traffic cone", "Fence" });
                Debug.Log($"DemoSetup: Loaded {controller.StreetPropPrefabs.Length} street prop prefabs.");
            }

            // ═══════════════════════════════════════════════════════════
            //  VEHICLE PREFABS
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.VehiclePrefabs = LoadPrefabsFromFolder(
                    $"{PrefabRoot}/Vehicles/Vehicle with Static Wheels");
                Debug.Log($"DemoSetup: Loaded {controller.VehiclePrefabs.Length} vehicle prefabs.");
            }

            // ═══════════════════════════════════════════════════════════
            //  MATERIALS — solid colors for procedural elements
            // ═══════════════════════════════════════════════════════════

            // Building materials (fallback for procedural mode)
            controller.BuildingWallMaterial = CreateSolidMaterial(matPath, "BuildingWallMat", shader,
                new Color(0.82f, 0.82f, 0.82f), 0.15f);
            controller.BuildingRoofMaterial = CreateSolidMaterial(matPath, "BuildingRoofMat", shader,
                new Color(0.45f, 0.43f, 0.42f), 0.1f);

            // Road materials — solid asphalt colors matching low-poly aesthetic
            controller.MotorwayMaterial = CreateSolidMaterial(matPath, "MotorwayMat", shader,
                new Color(0.20f, 0.20f, 0.22f), 0.08f);
            controller.PrimaryRoadMaterial = CreateSolidMaterial(matPath, "PrimaryRoadMat", shader,
                new Color(0.25f, 0.25f, 0.27f), 0.08f);
            controller.ResidentialRoadMaterial = CreateSolidMaterial(matPath, "ResidentialRoadMat", shader,
                new Color(0.30f, 0.30f, 0.32f), 0.08f);
            controller.FootpathMaterial = CreateSolidMaterial(matPath, "FootpathMat", shader,
                new Color(0.55f, 0.55f, 0.53f), 0.10f);
            controller.CrosswalkMaterial = CreateSolidMaterial(matPath, "CrosswalkMat", shader,
                new Color(0.85f, 0.85f, 0.82f), 0.08f);

            // General road / sidewalk
            controller.RoadMaterial = CreateSolidMaterial(matPath, "RoadMat", shader,
                new Color(0.22f, 0.22f, 0.24f), 0.05f);
            controller.SidewalkMaterial = CreateSolidMaterial(matPath, "SidewalkMat", shader,
                new Color(0.60f, 0.60f, 0.60f), 0.1f);

            // Ground & Park — solid colors matching low-poly style
            controller.GroundMaterial = CreateSolidMaterial(matPath, "GroundMat", shader,
                new Color(0.18f, 0.40f, 0.12f), 0.1f);
            controller.ParkMaterial = CreateSolidMaterial(matPath, "ParkMat", shader,
                new Color(0.18f, 0.55f, 0.12f), 0.05f);
            controller.WaterMaterial = CreateSolidMaterial(matPath, "WaterMat", shader,
                new Color(0.15f, 0.30f, 0.38f), 0.6f);

            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = controller.gameObject;

            string modeStr = hasSimplePoly ? "SimplePoly City prefabs" : "Procedural (fallback)";
            Debug.Log($"Demo Scene Setup Complete! Mode: {modeStr}. Open 'GeoCity3D > City Generator' to build a city.");
        }

        // ═══════════════════════════════════════════════════════════
        //  PREFAB LOADING HELPERS
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Load all .prefab files from a folder.
        /// If nameFilters is provided, only include prefabs whose name contains at least one filter string.
        /// </summary>
        private static GameObject[] LoadPrefabsFromFolder(string folderPath, string[] nameFilters = null)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                return new GameObject[0];

            List<GameObject> result = new List<GameObject>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                if (nameFilters != null && nameFilters.Length > 0)
                {
                    bool matches = false;
                    foreach (string filter in nameFilters)
                    {
                        if (prefab.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matches = true;
                            break;
                        }
                    }
                    if (!matches) continue;
                }

                result.Add(prefab);
            }

            return result.ToArray();
        }

        // ═══════════════════════════════════════════════════════════
        //  MATERIAL HELPERS
        // ═══════════════════════════════════════════════════════════

        private static Shader FindBestShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            return shader;
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
    }
}
