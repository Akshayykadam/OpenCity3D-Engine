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

        [MenuItem("GeoCity3D/Setup Scene", false, 2)]
        public static void Setup()
        {
            CityController controller = Object.FindFirstObjectByType<CityController>();
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
            // Use Residential Buildings Set FBX models for buildings
            string residentialPath = "Assets/Residential Buildings Set";
            if (AssetDatabase.IsValidFolder(residentialPath))
            {
                List<GameObject> buildingModels = new List<GameObject>();
                // Load all 10 FBX files by direct path
                for (int i = 1; i <= 10; i++)
                {
                    string fbxPath = $"{residentialPath}/Residential Buildings {i:D3}.fbx";
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    if (model != null)
                    {
                        buildingModels.Add(model);
                        Debug.Log($"  Loaded: {fbxPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"  NOT FOUND: {fbxPath}");
                    }
                }
                controller.BuildingPrefabs = buildingModels.ToArray();
                controller.BuildingGenerationMode = BuildingMode.Procedural;
                Debug.Log($"DemoSetup: Loaded {controller.BuildingPrefabs.Length} Residential Buildings Set FBX models (Default: Procedural mode).");
            }
            else
            {
                controller.BuildingPrefabs = new GameObject[0];
                controller.BuildingGenerationMode = BuildingMode.Procedural;
                Debug.LogWarning($"DemoSetup: Folder not found at '{residentialPath}', using procedural buildings.");
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
            //  BUSH, ROCK & GRASS PREFABS (parks & green land)
            // ═══════════════════════════════════════════════════════════
            if (hasSimplePoly)
            {
                controller.BushPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Natures",
                    new[] { "Bush", "Pot Bush" });
                controller.RockPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Natures",
                    new[] { "Rock" });
                controller.GrassPrefabs = LoadPrefabsFromFolder($"{PrefabRoot}/Natures",
                    new[] { "Grass Tile", "Grass Bar", "Grass Tile Small" });
                Debug.Log($"DemoSetup: Loaded {controller.BushPrefabs.Length} bush + {controller.RockPrefabs.Length} rock + {controller.GrassPrefabs.Length} grass prefabs.");
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

            string asphaltTex = "Assets/GeoCity3D/Textures/asphalt_road.jpg";
            string concreteTex = "Assets/GeoCity3D/Textures/concrete.jpg";

            // Road materials — textured asphalt colors matching modern low-poly aesthetic
            controller.MotorwayMaterial = CreateSolidMaterial(matPath, "MotorwayMat", shader,
                new Color(0.25f, 0.25f, 0.27f), 0.12f, asphaltTex, new Vector2(1f, 10f));
            controller.PrimaryRoadMaterial = CreateSolidMaterial(matPath, "PrimaryRoadMat", shader,
                new Color(0.30f, 0.30f, 0.32f), 0.10f, asphaltTex, new Vector2(1f, 8f));
            controller.ResidentialRoadMaterial = CreateSolidMaterial(matPath, "ResidentialRoadMat", shader,
                new Color(0.35f, 0.35f, 0.37f), 0.08f, asphaltTex, new Vector2(1f, 6f));
            controller.FootpathMaterial = CreateSolidMaterial(matPath, "FootpathMat", shader,
                new Color(0.60f, 0.60f, 0.58f), 0.10f, concreteTex, new Vector2(2f, 8f));
            controller.CrosswalkMaterial = CreateSolidMaterial(matPath, "CrosswalkMat", shader,
                new Color(0.92f, 0.92f, 0.88f), 0.08f);

            // General road / sidewalk
            controller.RoadMaterial = CreateSolidMaterial(matPath, "RoadMat", shader,
                new Color(0.28f, 0.28f, 0.30f), 0.08f, asphaltTex, new Vector2(1f, 6f));
            controller.SidewalkMaterial = CreateSolidMaterial(matPath, "SidewalkMat", shader,
                new Color(0.68f, 0.68f, 0.68f), 0.15f, concreteTex, new Vector2(2f, 8f));

            // Ground & Park — solid colors matching low-poly style
            controller.GroundMaterial = CreateSolidMaterial(matPath, "GroundMat", shader,
                new Color(0.18f, 0.40f, 0.12f), 0.1f);
            controller.ParkMaterial = CreateSolidMaterial(matPath, "ParkMat", shader,
                new Color(0.18f, 0.55f, 0.12f), 0.05f);
            controller.WaterMaterial = CreateWaterMaterialAsset(matPath, "WaterMat", shader);

            // ── Setup Atmosphere, Realistic Lighting, Reflection Probes & Aerial Fog ──
            GeoCity3D.Visuals.SceneSetup.Setup(500f);

            EditorUtility.SetDirty(controller);
            Selection.activeGameObject = controller.gameObject;

            string modeStr = hasSimplePoly ? "SimplePoly City prefabs" : "Procedural (fallback)";
            Debug.Log($"Demo Scene Setup Complete! Mode: {modeStr}. Open 'GeoCity3D > City Generator' to build a city.");
        }

        public static void ApplyBlueSkySkybox()
        {
            string matPath = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Materials/Polyverse Skies - Blue Sky.mat";
            Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (skyMat == null)
                skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/GeoCity3D/Materials/Skybox_BlueSky.mat");

            if (skyMat != null)
            {
                RenderSettings.skybox = skyMat;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.25f;
                DynamicGI.UpdateEnvironment();

                Camera mainCam = Camera.main;
                if (mainCam != null)
                    mainCam.clearFlags = CameraClearFlags.Skybox;

                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
                }

                Debug.Log("<color=green>[SKYBOX APPLIED] Polyverse Skies - Blue Sky is now active skybox!</color>");
            }
            else
            {
                Debug.LogWarning("[SKYBOX] Polyverse Skies - Blue Sky material not found!");
            }
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
            Color color, float smoothness, string texturePath = null, Vector2? tile = null)
        {
            string matAssetPath = $"{folder}/{matName}.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

            if (!string.IsNullOrEmpty(texturePath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    if (tile.HasValue)
                    {
                        mat.mainTextureScale = tile.Value;
                        if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tile.Value);
                        if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", tile.Value);
                    }
                }
            }

            mat.renderQueue = 2000;
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }

        private static Material CreateWaterMaterialAsset(string folder, string matName, Shader shader)
        {
            string matAssetPath = $"{folder}/{matName}.mat";

            if (AssetDatabase.LoadAssetAtPath<Material>(matAssetPath) != null)
                AssetDatabase.DeleteAsset(matAssetPath);

            Material mat = new Material(shader);
            mat.name = matName;

            // Vibrant translucent cyan-blue tint
            Color waterColor = new Color(0.10f, 0.48f, 0.70f, 0.82f);
            mat.color = waterColor;

            // High gloss & subtle metallic sheen for light reflections
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.96f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);

            // Use persistent water texture asset on disk so it persists across Play Mode and domain reloads
            Texture2D waterTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeoCity3D/Textures/water.jpg");
            if (waterTex == null)
                waterTex = TextureGenerator.CreateWaterTexture(512, 512);

            mat.mainTexture = waterTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", waterTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", waterTex);

            // Transparency configuration for Standard / Lit
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // URP
            mat.SetFloat("_Mode", 3f); // Standard transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.enableInstancing = true;

            AssetDatabase.CreateAsset(mat, matAssetPath);
            return mat;
        }
    }
}
