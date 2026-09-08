using UnityEditor;
using UnityEngine;
using GeoCity3D.Network;
using GeoCity3D.Parsing;
using GeoCity3D.Data;
using GeoCity3D.Geometry;
using GeoCity3D.Coordinates;
using GeoCity3D.Visuals;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GeoCity3D.Editor
{
    public class CityGeneratorWindow : EditorWindow
    {
        private double _latitude = 50.113048581605035;
        private double _longitude = 8.67213038757014;
        private float _radius = 1000f;
        
        private CityController _cityController;
        private bool _isGenerating = false;

        private int _selectedTab = 0;
        private readonly string[] _tabs = { "Real-World OSM", "Procedural Map" };
        private float _proceduralMapRadius = 350f;
        private float _buildingCornerRadius = 1.8f;
        private bool _includeRiver = true;
        private bool _includeLake = true;
        private bool _includeVehicles = false;
        private bool _includeTrees = true;
        private bool _includeGrass = true;
        private bool _includeStones = true;
        private bool _includeSignals = false;
        private float _roadWidthScale = 1.0f;
        private NatureMode _natureMode = NatureMode.ProceduralMesh;

        [MenuItem("GeoCity3D/City Generator", false, 1)]
        public static void ShowWindow()
        {
            GetWindow<CityGeneratorWindow>("City Generator");
        }

        private void OnEnable()
        {
            EnsureTagManagerLayers();
            EnsureCityController();
        }

        private static void EnsureTagManagerLayers()
        {
            try
            {
                var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                if (tagManagerAssets == null || tagManagerAssets.Length == 0) return;

                SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
                SerializedProperty layersProp = tagManager.FindProperty("layers");
                if (layersProp == null) return;

                bool changed = false;
                changed |= EnsureLayerInProperty(layersProp, "Grass", 8);
                changed |= EnsureLayerInProperty(layersProp, "Props", 9);

                if (changed)
                {
                    tagManager.ApplyModifiedProperties();
                }
            }
            catch { /* Skip gracefully if asset unavailable */ }
        }

        private static bool EnsureLayerInProperty(SerializedProperty layersProp, string name, int preferredIndex)
        {
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == name) return false;
            }

            if (preferredIndex < layersProp.arraySize && string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                layersProp.GetArrayElementAtIndex(preferredIndex).stringValue = name;
                return true;
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var p = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = name;
                    return true;
                }
            }
            return false;
        }

        private static void AssignLayerIfFound(GameObject go, string layerName)
        {
            if (go == null) return;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
        }

        private static bool HasValidPrefabs(GameObject[] arr)
        {
            if (arr == null || arr.Length == 0) return false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null) return true;
            }
            return false;
        }

        private static GameObject[] CleanPrefabArray(GameObject[] arr)
        {
            if (arr == null) return new GameObject[0];
            List<GameObject> valid = new List<GameObject>();
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null) valid.Add(arr[i]);
            }
            return valid.ToArray();
        }

        private static void SanitizeController(CityController controller)
        {
            if (controller == null) return;
            bool modified = false;

            void CheckAndClean(ref GameObject[] array)
            {
                if (array != null)
                {
                    var cleaned = CleanPrefabArray(array);
                    if (cleaned.Length != array.Length)
                    {
                        array = cleaned;
                        modified = true;
                    }
                }
            }

            CheckAndClean(ref controller.BuildingPrefabs);
            CheckAndClean(ref controller.TreePrefabs);
            CheckAndClean(ref controller.BushPrefabs);
            CheckAndClean(ref controller.RockPrefabs);
            CheckAndClean(ref controller.GrassPrefabs);
            CheckAndClean(ref controller.StreetLightPrefabs);
            CheckAndClean(ref controller.TrafficSignalPrefabs);
            CheckAndClean(ref controller.StreetPropPrefabs);
            CheckAndClean(ref controller.VehiclePrefabs);

            if (modified)
            {
                EditorUtility.SetDirty(controller);
            }
        }

        private void EnsureCityController()
        {
            if (_cityController == null)
            {
                _cityController = Object.FindFirstObjectByType<CityController>();
            }

            if (_cityController != null)
            {
                SanitizeController(_cityController);
            }

            if (_cityController != null && (_cityController.GrassPrefabs == null || _cityController.GrassPrefabs.Length == 0))
            {
                string[] guids = AssetDatabase.FindAssets("Natures_Grass Tile Small t:Prefab");
                if (guids.Length > 0)
                {
                    List<GameObject> grassList = new List<GameObject>();
                    string[] names = new[] { "Natures_Grass Tile", "Natures_Grass Bar", "Natures_Grass Tile Small" };
                    foreach (var name in names)
                    {
                        string[] matches = AssetDatabase.FindAssets($"{name} t:Prefab");
                        foreach (var g in matches)
                        {
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                            if (prefab != null && !grassList.Contains(prefab)) grassList.Add(prefab);
                        }
                    }
                    if (grassList.Count > 0)
                    {
                        _cityController.GrassPrefabs = grassList.ToArray();
                        EditorUtility.SetDirty(_cityController);
                    }
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("GeoCity3D City Generator", EditorStyles.boldLabel);

            EnsureCityController();

            _cityController = (CityController)EditorGUILayout.ObjectField("City Controller", _cityController, typeof(CityController), true);

            if (_cityController == null)
            {
                EditorGUILayout.HelpBox("No City Controller found in the active scene.\nClick below to create and configure one with materials automatically.", MessageType.Warning);
                if (GUILayout.Button("Setup City Controller in Scene", GUILayout.Height(24)))
                {
                    DemoSetup.Setup();
                    _cityController = Object.FindFirstObjectByType<CityController>();
                }
                EditorGUILayout.Space(4);
            }
            else
            {
                EditorGUILayout.Space(2);
            }

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            EditorGUILayout.Space(8);

            if (_selectedTab == 0)
            {
                GUILayout.Label("Real-World OSM Ingestion", EditorStyles.boldLabel);
                _latitude = EditorGUILayout.DoubleField("Latitude", _latitude);
                _longitude = EditorGUILayout.DoubleField("Longitude", _longitude);
                _radius = EditorGUILayout.FloatField("Radius (m)", _radius);

                if (_cityController != null)
                {
                    _cityController.BuildingGenerationMode = (BuildingMode)EditorGUILayout.EnumPopup("Building Generation Mode", _cityController.BuildingGenerationMode);
                }

                EditorGUILayout.Space(6);
                DrawCityElementsOptions();

                EditorGUILayout.Space(8);
                if (GUILayout.Button("Generate City (From OSM)", GUILayout.Height(30)))
                {
                    EnsureCityController();
                    if (_cityController == null)
                    {
                        Debug.Log("No City Controller in scene. Setting up scene CityController automatically...");
                        DemoSetup.Setup();
                        _cityController = Object.FindFirstObjectByType<CityController>();
                    }

                    if (_cityController == null)
                    {
                        Debug.LogError("Please assign a City Controller scene object with materials!");
                        return;
                    }
                    
                    if (!_isGenerating)
                    {
                        _isGenerating = true;
                        SimpleEditorCoroutine.Start(GenerateCity());
                    }
                }
            }
            else
            {
                GUILayout.Label("Synthetic Procedural Map Generator", EditorStyles.boldLabel);
                _proceduralMapRadius = EditorGUILayout.FloatField("Map Radius (m)", _proceduralMapRadius);
                _buildingCornerRadius = EditorGUILayout.Slider("Building Corner Radius (m)", _buildingCornerRadius, 0.5f, 3.5f);
                _includeRiver = EditorGUILayout.Toggle("Include Meandering River", _includeRiver);
                _includeLake = EditorGUILayout.Toggle("Include Scenic Lake", _includeLake);

                EditorGUILayout.Space(6);
                DrawCityElementsOptions();

                EditorGUILayout.Space(8);
                if (GUILayout.Button("Generate Procedural Map", GUILayout.Height(30)))
                {
                    EnsureCityController();
                    if (_cityController == null)
                    {
                        Debug.Log("No City Controller in scene. Setting up scene CityController automatically...");
                        DemoSetup.Setup();
                        _cityController = Object.FindFirstObjectByType<CityController>();
                    }

                    if (_cityController == null)
                    {
                        Debug.LogError("Please assign a City Controller scene object with materials!");
                        return;
                    }
                    
                    if (!_isGenerating)
                    {
                        _isGenerating = true;
                        SimpleEditorCoroutine.Start(GenerateProceduralMap());
                    }
                }
            }
        }

        private void DrawCityElementsOptions()
        {
            EditorGUILayout.LabelField("City Elements (Select / Unselect):", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
            {
                _includeVehicles = true;
                _includeTrees = true;
                _includeGrass = true;
                _includeStones = true;
                _includeSignals = true;
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight))
            {
                _includeVehicles = false;
                _includeTrees = false;
                _includeGrass = false;
                _includeStones = false;
                _includeSignals = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            _roadWidthScale = EditorGUILayout.Slider("Road Width Scale", _roadWidthScale, 0.8f, 2.0f);
            _includeVehicles = EditorGUILayout.Toggle("Vehicles (Cars)", _includeVehicles);
            _includeTrees = EditorGUILayout.Toggle("Trees & Nature", _includeTrees);
            _includeGrass = EditorGUILayout.Toggle("Grass & Ground Cover", _includeGrass);
            _includeStones = EditorGUILayout.Toggle("Stones & Rocks", _includeStones);
            if (_includeTrees || _includeStones || _includeGrass)
            {
                _natureMode = (NatureMode)EditorGUILayout.EnumPopup("Nature Mode", _natureMode);
            }
            _includeSignals = EditorGUILayout.Toggle("Signals & Street Lights", _includeSignals);
        }

        // ── Solid color material — renders BOTH sides so geometry never looks hollow ──
        private Material CreateSolidMaterial(Shader shader, Color color, float smoothness = 0.3f)
        {
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;
            return mat;
        }

        // ── Textured material from a procedural texture ──
        private Material CreateTexturedMaterial(Shader shader, Texture2D texture, float smoothness = 0.05f)
        {
            Material mat = new Material(shader);
            mat.mainTexture = texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            mat.color = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;
            return mat;
        }

        // ── Reflective translucent water material ──
        private Material CreateWaterMaterial(Shader shader)
        {
            Material mat = new Material(shader);
            mat.name = "WaterMat_Procedural";
            Color waterColor = new Color(0.10f, 0.48f, 0.70f, 0.82f); // Vibrant translucent cyan-blue
            mat.color = waterColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", waterColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", waterColor);

            Texture2D waterTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeoCity3D/Textures/water.jpg");
            if (waterTex == null)
                waterTex = TextureGenerator.CreateWaterTexture(512, 512);

            mat.mainTexture = waterTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", waterTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", waterTex);

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.96f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);

            // Enable transparency
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // URP Transparent
            mat.SetFloat("_Mode", 3f); // Standard shader transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000; // Transparent queue
            mat.enableInstancing = true;
            return mat;
        }

        // ── Deep reflective tinted glass material for building windows ──
        private Material CreateGlassMaterial(Shader shader)
        {
            Material mat = new Material(shader);
            mat.name = "BuildingGlass_Procedural";
            Color glassColor = new Color(0.12f, 0.20f, 0.32f, 1.0f);
            mat.color = glassColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", glassColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", glassColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.92f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.92f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.50f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.renderQueue = 2000;
            mat.enableInstancing = true;
            return mat;
        }

        // ── Textured material with normal map ──
        private Material CreateTexturedMaterial(Shader shader, Texture2D texture, Texture2D normalMap, float smoothness = 0.05f)
        {
            Material mat = CreateTexturedMaterial(shader, texture, smoothness);
            if (normalMap != null)
            {
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetFloat("_BumpScale", 1.0f);
                }
            }
            return mat;
        }

        private Shader FindBestShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            return shader;
        }

        private IEnumerator GenerateCity()
        {
            Debug.Log("Starting City Generation...");
            EnsureCityController();
            SanitizeController(_cityController);
            
            // 1. Fetch Data
            string osmData = null;
            bool failed = false;

            OverpassClient client = new OverpassClient();
            yield return client.GetMapData(_latitude, _longitude, _radius, 
                (data) => osmData = data, 
                (error) => { Debug.LogError("Download failed: " + error); failed = true; }
            );

            if (failed || string.IsNullOrEmpty(osmData))
            {
                if (!failed) Debug.LogError("OSM Data is null or empty after download.");
                _isGenerating = false;
                yield break;
            }

            Debug.Log($"Data downloaded. Size: {osmData.Length} chars.");

            // 2. Parse Data
            OsmXmlParser parser = new OsmXmlParser();
            OsmData data = parser.Parse(osmData);
            Debug.Log($"Parsed: {data.Nodes.Count} nodes, {data.Ways.Count} ways.");

            // 3. Setup Origin
            var shifter = FindFirstObjectByType<OriginShifter>();
            if (shifter == null)
            {
                 GameObject shifterObj = new GameObject("OriginShifter");
                 shifter = shifterObj.AddComponent<OriginShifter>();
            }
            shifter.SetOrigin(_latitude, _longitude);

            // 4. Find shader
            Shader shader = FindBestShader();
            if (shader == null)
            {
                Debug.LogError("No valid shader found!");
                _isGenerating = false;
                yield break;
            }

            GrassBuilder.ResetMaterialPool();

            // ═══════════════════════════════════════════════════════════
            // 5. MATERIALS — Procedural textured materials
            // ═══════════════════════════════════════════════════════════

            // ── Building Materials ──
            // Textured architectural facade with normal-mapped window frames, sills, and reflective glass
            Texture2D facadeNormalMap = TextureGenerator.CreateFacadeNormalMap();
            yield return null;
            Material buildingMat = _cityController.BuildingWallMaterial != null 
                ? _cityController.BuildingWallMaterial 
                : CreateTexturedMaterial(shader, TextureGenerator.CreateFacadeTexture(512, 512, new Color(0.92f, 0.91f, 0.89f)), facadeNormalMap, 0.25f);
            yield return null;
            Material roofMat = _cityController.BuildingRoofMaterial != null 
                ? _cityController.BuildingRoofMaterial 
                : CreateTexturedMaterial(shader, TextureGenerator.CreateRoofTexture(256, 256, new Color(0.38f, 0.38f, 0.40f)), 0.15f);
            yield return null;
            Material windowMat = _cityController.BuildingWindowMaterial != null
                ? _cityController.BuildingWindowMaterial
                : CreateGlassMaterial(shader);
            yield return null;

            // Roads
            Texture2D roadNormalMap = TextureGenerator.CreateAsphaltNormalMap();
            yield return null;
            Material motorwayMat = _cityController.MotorwayMaterial != null ? _cityController.MotorwayMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateMotorwayTexture(), roadNormalMap, 0.05f);
            yield return null;
            Material primaryRoadMat = _cityController.PrimaryRoadMaterial != null ? _cityController.PrimaryRoadMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreatePrimaryRoadTexture(), roadNormalMap, 0.05f);
            yield return null;
            Material residentialRoadMat = _cityController.ResidentialRoadMaterial != null ? _cityController.ResidentialRoadMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateResidentialRoadTexture(), roadNormalMap, 0.05f);
            yield return null;
            Material footpathMat = _cityController.FootpathMaterial != null ? _cityController.FootpathMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateFootpathTexture(), 0.05f);
            yield return null;
            Material crosswalkMat = _cityController.CrosswalkMaterial != null ? _cityController.CrosswalkMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateCrosswalkTexture(), 0.05f);
            yield return null;

            Dictionary<string, Material> roadMaterials = new Dictionary<string, Material>
            {
                { "motorway", motorwayMat },
                { "primary", primaryRoadMat },
                { "residential", residentialRoadMat },
                { "footpath", footpathMat }
            };

            // Infrastructure & Areas
            Material sidewalkMat = _cityController.SidewalkMaterial != null ? _cityController.SidewalkMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateSidewalkTexture(), 0.1f);
            yield return null;
            Material parkMat = _cityController.ParkMaterial != null ? _cityController.ParkMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateParkTexture(), 0.05f);
            yield return null;
            Material waterMat = _cityController.WaterMaterial != null ? _cityController.WaterMaterial : CreateWaterMaterial(shader);
            yield return null;
            Material beachMat = CreateSolidMaterial(shader, new Color(0.82f, 0.72f, 0.52f), 0.05f);
            Material groundMat = _cityController.GroundMaterial != null ? _cityController.GroundMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateGroundTexture(), 0.1f);
            yield return null;
            Material platformMat = CreateSolidMaterial(shader, new Color(0.28f, 0.28f, 0.30f), 0.15f);
            Material intersectionMat = _cityController.RoadMaterial != null ? _cityController.RoadMaterial : CreateSolidMaterial(shader, new Color(0.22f, 0.22f, 0.24f), 0.05f);

            // 6. Generate Geometry
            GameObject cityRoot = new GameObject("GeneratedCity");
            cityRoot.transform.position = Vector3.zero;

            GameObject buildingsParent = new GameObject("Buildings");
            buildingsParent.transform.SetParent(cityRoot.transform);
            GameObject roadsParent = new GameObject("Roads");
            roadsParent.transform.SetParent(cityRoot.transform);
            GameObject intersectionsParent = new GameObject("Intersections");
            intersectionsParent.transform.SetParent(cityRoot.transform);
            GameObject parksParent = new GameObject("Parks");
            parksParent.transform.SetParent(cityRoot.transform);
            GameObject waterParent = new GameObject("Water");
            waterParent.transform.SetParent(cityRoot.transform);
            GameObject treesParent = new GameObject("Trees");
            treesParent.transform.SetParent(cityRoot.transform);
            GameObject stonesParent = new GameObject("Stones");
            stonesParent.transform.SetParent(cityRoot.transform);
            GameObject beachesParent = new GameObject("Beaches");
            beachesParent.transform.SetParent(cityRoot.transform);
            GameObject vehiclesParent = new GameObject("Vehicles");
            vehiclesParent.transform.SetParent(cityRoot.transform);
            GameObject propsParent = new GameObject("StreetProps");
            propsParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(propsParent, "Props");
            GameObject signalsParent = new GameObject("TrafficSignals");
            signalsParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(signalsParent, "Props");
            GameObject lotFillParent = new GameObject("LotFill");
            lotFillParent.transform.SetParent(cityRoot.transform);
            GameObject grassParent = new GameObject("Grass");
            grassParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(grassParent, "Grass");

            int buildingCount = 0, roadCount = 0, parkCount = 0, waterCount = 0, beachCount = 0, treeCount = 0, stoneCount = 0;
            int intersectionCount = 0, vehicleCount = 0, propCount = 0, signalCount = 0, lotFillCount = 0, grassCount = 0;

            // Clear intersection data from previous generation
            RoadBuilder.ClearIntersectionData();
            StreetFurnitureBuilder.ResetMaterialPool();
            TreeBuilder.ResetMaterialPool();
            RockBuilder.ResetMaterialPool();
            GrassBuilder.ResetMaterialPool();
            LODBuilder.ResetPalette();

            List<Bounds> buildingBounds = new List<Bounds>();
            List<Bounds> roadBounds = new List<Bounds>();
            List<Bounds> beachBounds = new List<Bounds>();
            List<OsmWay> highwayWays = new List<OsmWay>();
            List<WaterAreaInfo> waterAreas = new List<WaterAreaInfo>();
            List<WaterwayInfo> waterways = new List<WaterwayInfo>();
            List<Vector3> parkCenters = new List<Vector3>();
            List<float> parkSizes = new List<float>();
            List<List<Vector3>> parkPolys = new List<List<Vector3>>();

            // Pre-pass: Catalog all water bodies (polygons and linear waterways) upfront
            // so buildings, parks, street trees, street furniture, and lot filler never spawn inside water.
            foreach (var way in data.Ways)
            {
                if (IsWaterArea(way))
                {
                    List<Vector3> polygon = new List<Vector3>();
                    foreach (long nodeId in way.NodeIds)
                    {
                        if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                            polygon.Add(shifter.GetLocalPosition(node.Latitude, node.Longitude));
                    }
                    if (polygon.Count >= 3)
                        waterAreas.Add(new WaterAreaInfo(polygon));
                }
                else if (IsLinearWaterway(way))
                {
                    List<Vector3> path = new List<Vector3>();
                    foreach (long nodeId in way.NodeIds)
                    {
                        if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                            path.Add(shifter.GetLocalPosition(node.Latitude, node.Longitude));
                    }
                    if (path.Count >= 2)
                        waterways.Add(new WaterwayInfo(path, DetermineRiverWidth(way)));
                }
            }

            // ── Resolve & Deduplicate Buildings (OSM 3D Simple Buildings Specification) ──
            HashSet<long> validBuildingWayIds = ResolveAndDeduplicateBuildings(data, shifter);

            foreach (var way in data.Ways)
            {
                if (way.HasTag("building") || way.HasTag("building:part"))
                {
                    if (!validBuildingWayIds.Contains(way.Id)) continue;

                    GameObject building = null;

                    bool isLandmarkOrMultipolygon = way.Id < 0 || way.HasTag("building:part");
                    bool hasBuildingPrefabs = HasValidPrefabs(_cityController.BuildingPrefabs);
                    if (_cityController.BuildingGenerationMode == BuildingMode.Prefab
                        && hasBuildingPrefabs
                        && !isLandmarkOrMultipolygon)
                    {
                        // ── PREFAB MODE — Residential Buildings Set (FBX, needs -90° X rotation) ──
                        Vector3 center = Vector3.zero;
                        int nodeCount = 0;
                        List<Vector3> footprintPts = new List<Vector3>();
                        foreach (long nodeId in way.NodeIds)
                        {
                            if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                            {
                                Vector3 p = shifter.GetLocalPosition(node.Latitude, node.Longitude);
                                footprintPts.Add(p);
                                center += p;
                                nodeCount++;
                            }
                        }

                        if (nodeCount > 0)
                        {
                            center /= nodeCount;
                            center.y = 0f;

                            // Compute footprint size
                            float minX = float.MaxValue, maxX = float.MinValue;
                            float minZ = float.MaxValue, maxZ = float.MinValue;
                            foreach (var p in footprintPts)
                            {
                                if (p.x < minX) minX = p.x;
                                if (p.x > maxX) maxX = p.x;
                                if (p.z < minZ) minZ = p.z;
                                if (p.z > maxZ) maxZ = p.z;
                            }
                            float footW = maxX - minX;
                            float footD = maxZ - minZ;
                            if (footW < 3f || footD < 3f) continue;

                            // Road-aligned rotation from longest footprint edge
                            float yAngle = 0f;
                            if (footprintPts.Count >= 2)
                            {
                                float longestEdge = 0f;
                                Vector3 longestDir = Vector3.forward;
                                for (int e = 0; e < footprintPts.Count - 1; e++)
                                {
                                    float edgeLen = Vector3.Distance(footprintPts[e], footprintPts[e + 1]);
                                    if (edgeLen > longestEdge)
                                    {
                                        longestEdge = edgeLen;
                                        longestDir = (footprintPts[e + 1] - footprintPts[e]).normalized;
                                    }
                                }
                                yAngle = Mathf.Atan2(longestDir.x, longestDir.z) * Mathf.Rad2Deg;
                            }

                            var validBuildings = CleanPrefabArray(_cityController.BuildingPrefabs);
                            if (validBuildings.Length > 0)
                            {
                                GameObject prefab = validBuildings[Random.Range(0, validBuildings.Length)];
                                if (prefab != null)
                                {
                                    // FBX models need -90° X rotation (Y-up to Z-forward)
                                    building = Instantiate(prefab, center, Quaternion.Euler(-90f, yAngle, 0f));
                                    building.name = $"Building_{way.Id}";
                                    // FBX default scale is 0.01, so 100x to get Unity scale
                                    building.transform.localScale = Vector3.one * 100f;

                                    // Fit to footprint
                                    Renderer[] mrs = building.GetComponentsInChildren<Renderer>();
                                    if (mrs.Length > 0)
                                    {
                                        Bounds mb = mrs[0].bounds;
                                        for (int i = 1; i < mrs.Length; i++)
                                            mb.Encapsulate(mrs[i].bounds);

                                        float sX = (mb.size.x > 0.1f) ? (footW / mb.size.x) : 1f;
                                        float sZ = (mb.size.z > 0.1f) ? (footD / mb.size.z) : 1f;
                                        float fitScale = Mathf.Clamp(Mathf.Min(sX, sZ), 0.3f, 2f);
                                        building.transform.localScale = Vector3.one * 100f * fitScale;

                                        // Ground the building
                                        mrs = building.GetComponentsInChildren<Renderer>();
                                        if (mrs.Length > 0)
                                        {
                                            Bounds fb = mrs[0].bounds;
                                            for (int i = 1; i < mrs.Length; i++)
                                                fb.Encapsulate(mrs[i].bounds);
                                            Vector3 pos = building.transform.position;
                                            pos.y -= fb.min.y;
                                            building.transform.position = pos;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (building == null)
                    {
                        // ── PROCEDURAL MODE ── identical architectural structures with rounded corners
                        building = BuildingBuilder.Build(way, data, buildingMat, roofMat, shifter, windowMat);
                    }

                    if (building != null)
                    {
                        building.transform.SetParent(buildingsParent.transform);
                        buildingCount++;

                        // Add LOD system to each building
                        LODBuilder.AddLOD(building);

                        Renderer[] renderers = building.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            Bounds totalBounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++)
                                totalBounds.Encapsulate(renderers[i].bounds);
                            buildingBounds.Add(totalBounds);
                        }
                    }
                }
                else if (way.HasTag("highway"))
                {
                    string hwType = (way.GetTag("highway") ?? "").ToLower();
                    if (!RoadBuilder.FootpathTypes.Contains(hwType))
                    {
                        highwayWays.Add(way);
                    }
                }
                else if (IsArea(way, "park") || IsArea(way, "grass") || IsArea(way, "forest")
                    || IsArea(way, "garden") || IsArea(way, "meadow"))
                {
                    GameObject park = AreaBuilder.Build(way, data, parkMat, shifter, 0.05f, "Park");
                    if (park != null)
                    {
                        park.transform.SetParent(parksParent.transform);
                        parkCount++;

                        Vector3 center = Vector3.zero;
                        int nodeCount = 0;
                        float maxDist = 0;
                        List<Vector3> parkPoints = new List<Vector3>();
                        foreach (long nodeId in way.NodeIds)
                        {
                            if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                            {
                                Vector3 pos = shifter.GetLocalPosition(node.Latitude, node.Longitude);
                                parkPoints.Add(pos);
                                center += pos;
                                nodeCount++;
                            }
                        }
                        if (nodeCount > 0)
                        {
                            center /= nodeCount;
                            foreach (var p in parkPoints)
                                maxDist = Mathf.Max(maxDist, Vector3.Distance(center, p));
                            parkCenters.Add(center);
                            parkSizes.Add(maxDist);
                            parkPolys.Add(parkPoints);
                        }
                    }
                }
                else if (IsBeachArea(way))
                {
                    GameObject beach = AreaBuilder.Build(way, data, beachMat, shifter, 0.02f, "Beach");
                    if (beach != null)
                    {
                        beach.transform.SetParent(beachesParent.transform);
                        beachCount++;

                        Renderer[] bRenderers = beach.GetComponentsInChildren<Renderer>();
                        if (bRenderers.Length > 0)
                        {
                            Bounds bb = bRenderers[0].bounds;
                            for (int bi = 1; bi < bRenderers.Length; bi++)
                                bb.Encapsulate(bRenderers[bi].bounds);
                            beachBounds.Add(bb);
                        }
                    }
                }
                else if (IsWaterArea(way))
                {
                    GameObject water = WaterBuilder.BuildLake(way, data, waterMat, shifter, "Lake");
                    if (water != null)
                    {
                        water.transform.SetParent(waterParent.transform);
                        waterCount++;
                    }
                }
                else if (IsLinearWaterway(way))
                {
                    float riverWidth = DetermineRiverWidth(way);
                    GameObject river = WaterBuilder.BuildRiver(way, data, waterMat, shifter, riverWidth);
                    if (river != null)
                    {
                        river.transform.SetParent(waterParent.transform);
                        waterCount++;
                    }
                }
            }

            // 6b. Build roads with intelligent bridge chaining (eliminates mid-span dips)
            List<GameObject> builtRoads = RoadBuilder.BuildRoadNetwork(highwayWays, data, roadMaterials, sidewalkMat, shifter, 9.0f, _roadWidthScale);
            foreach (var road in builtRoads)
            {
                if (road != null)
                {
                    road.transform.SetParent(roadsParent.transform);
                    roadCount++;

                    // Track road bounds for lot filler and grass scattering
                    Renderer[] roadRenderers = road.GetComponentsInChildren<Renderer>();
                    if (roadRenderers.Length > 0)
                    {
                        Bounds rb = roadRenderers[0].bounds;
                        for (int ri = 1; ri < roadRenderers.Length; ri++)
                            rb.Encapsulate(roadRenderers[ri].bounds);
                        roadBounds.Add(rb);
                    }
                }
            }

            // 7. Generate intersection fills where roads meet
            List<Vector3> intersectionCenters;
            intersectionCount = GenerateIntersections(
                intersectionsParent.transform,
                highwayWays,
                data,
                shifter,
                roadMaterials,
                intersectionMat,
                sidewalkMat,
                out intersectionCenters);

            // ── Determine generation modes ──
            bool useProceduralNature = (_natureMode == NatureMode.ProceduralMesh);
            bool hasTreePrefabs = !useProceduralNature && HasValidPrefabs(_cityController.TreePrefabs);
            bool hasBushPrefabs = !useProceduralNature && HasValidPrefabs(_cityController.BushPrefabs);
            bool hasRockPrefabs = !useProceduralNature && HasValidPrefabs(_cityController.RockPrefabs);
            bool hasGrassPrefabs = !useProceduralNature && HasValidPrefabs(_cityController.GrassPrefabs);
            bool hasLightPrefabs = HasValidPrefabs(_cityController.StreetLightPrefabs);
            bool hasSignalPrefabs = HasValidPrefabs(_cityController.TrafficSignalPrefabs);
            bool hasPropPrefabs = HasValidPrefabs(_cityController.StreetPropPrefabs);
            bool hasVehiclePrefabs = HasValidPrefabs(_cityController.VehiclePrefabs);

            int realTreesPlaced = 0;
            int realRocksPlaced = 0;

            // ── 8a. REAL-WORLD INDIVIDUAL TREES (OSM node[natural=tree]) ──
            if (_includeTrees && data.TaggedNodes != null)
            {
                foreach (var node in data.TaggedNodes)
                {
                    if (node.GetTag("natural") != "tree") continue;

                    Vector3 pos = shifter.GetLocalPosition(node.Latitude, node.Longitude);
                    if (IsInsideAnyBuilding(pos, buildingBounds) ||
                        WaterBuilder.IsPointInWater(pos, waterAreas, waterways, 1.0f) ||
                        RoadSpatialIndex.IsPointOnRoad(pos, 2.0f))
                        continue;

                    GameObject treeObj = hasTreePrefabs
                        ? TreeBuilder.BuildPrefab(pos, _cityController.TreePrefabs, Random.Range(0.7f, 1.2f))
                        : TreeBuilder.BuildFromOsm(pos, node, shader);

                    if (treeObj != null)
                    {
                        treeObj.transform.SetParent(treesParent.transform);
                        treeCount++;
                        realTreesPlaced++;
                    }
                }
            }

            // ── 8b. REAL-WORLD TREE ROWS (OSM way[natural=tree_row]) ──
            if (_includeTrees)
            {
                foreach (var way in data.Ways)
                {
                    if (way.GetTag("natural") != "tree_row") continue;

                    for (int i = 0; i < way.NodeIds.Count; i++)
                    {
                        if (!data.Nodes.TryGetValue(way.NodeIds[i], out OsmNode node)) continue;
                        Vector3 pos = shifter.GetLocalPosition(node.Latitude, node.Longitude);
                        if (IsInsideAnyBuilding(pos, buildingBounds) ||
                            WaterBuilder.IsPointInWater(pos, waterAreas, waterways, 1.0f) ||
                            RoadSpatialIndex.IsPointOnRoad(pos, 2.0f))
                            continue;

                        GameObject treeObj = hasTreePrefabs
                            ? TreeBuilder.BuildPrefab(pos, _cityController.TreePrefabs, Random.Range(0.8f, 1.2f))
                            : TreeBuilder.Build(pos, shader, Random.Range(0.8f, 1.2f));

                        if (treeObj != null)
                        {
                            treeObj.transform.SetParent(treesParent.transform);
                            treeCount++;
                            realTreesPlaced++;
                        }
                    }
                }
            }

            // ── 8c. REAL-WORLD ROCKS & BOULDERS (OSM node[natural=rock|bare_rock|stone]) ──
            if (_includeStones && data.TaggedNodes != null)
            {
                foreach (var node in data.TaggedNodes)
                {
                    string natural = (node.GetTag("natural") ?? "").ToLower();
                    if (natural != "rock" && natural != "bare_rock" && natural != "stone") continue;

                    Vector3 pos = shifter.GetLocalPosition(node.Latitude, node.Longitude);
                    if (IsInsideAnyBuilding(pos, buildingBounds) ||
                        WaterBuilder.IsPointInWater(pos, waterAreas, waterways, 0.4f) ||
                        RoadSpatialIndex.IsPointOnRoad(pos, 1.5f))
                        continue;

                    GameObject rockObj = hasRockPrefabs
                        ? TreeBuilder.BuildPrefab(pos, _cityController.RockPrefabs, Random.Range(0.6f, 1.5f))
                        : RockBuilder.BuildFromOsm(pos, node, shader);

                    if (rockObj != null)
                    {
                        rockObj.transform.SetParent(stonesParent.transform);
                        stoneCount++;
                        realRocksPlaced++;
                    }
                }
            }

            // ── 8d. PARKS & WOODLANDS (Procedural Mesh or Prefab) ──
            if (_includeTrees || _includeStones || _includeGrass)
            {
                for (int i = 0; i < parkCenters.Count; i++)
                {
                    float parkRadius = Mathf.Max(parkSizes[i] * 0.85f, 8f);
                    int treeCountInPark = Mathf.Clamp(Mathf.RoundToInt(parkRadius * parkRadius * 0.04f), 6, 60);

                    // ── Grass in park / green land ──
                    if (_includeGrass)
                    {
                        int grassTuftsInPark = Mathf.Clamp(Mathf.RoundToInt(parkRadius * parkRadius * 0.38f), 35, 750);
                        List<GameObject> grassObjects;
                        if (i < parkPolys.Count && parkPolys[i].Count >= 3)
                        {
                            grassObjects = GrassBuilder.ScatterInPolygon(
                                parkPolys[i], grassTuftsInPark,
                                hasGrassPrefabs ? _cityController.GrassPrefabs : null,
                                shader, 0.05f);
                        }
                        else
                        {
                            grassObjects = GrassBuilder.ScatterInCircle(
                                parkCenters[i], parkRadius, grassTuftsInPark,
                                hasGrassPrefabs ? _cityController.GrassPrefabs : null,
                                shader, 0.05f);
                        }

                        foreach (var g in grassObjects)
                        {
                            if (IsInsideAnyBuilding(g.transform.position, buildingBounds) ||
                                WaterBuilder.IsPointInWater(g.transform.position, waterAreas, waterways, 0.5f) ||
                                RoadSpatialIndex.IsPointOnRoad(g.transform.position, 0.4f))
                            {
                                Object.DestroyImmediate(g);
                            }
                            else
                            {
                                g.transform.SetParent(grassParent.transform);
                                grassCount++;
                            }
                        }
                    }

                    if (hasTreePrefabs || hasRockPrefabs)
                    {
                        GameObject[] tp = _includeTrees ? _cityController.TreePrefabs : null;
                        GameObject[] bp = _includeTrees ? _cityController.BushPrefabs : null;
                        GameObject[] rp = _includeStones ? _cityController.RockPrefabs : null;
                        List<GameObject> parkNature = TreeBuilder.ScatterParkNature(
                            parkCenters[i], parkRadius, treeCountInPark,
                            tp, bp, rp);
                        foreach (var obj in parkNature)
                        {
                            if (IsInsideAnyBuilding(obj.transform.position, buildingBounds) ||
                                WaterBuilder.IsPointInWater(obj.transform.position, waterAreas, waterways, 1.2f) ||
                                RoadSpatialIndex.IsPointOnRoad(obj.transform.position, 2.0f))
                            {
                                Object.DestroyImmediate(obj);
                            }
                            else
                            {
                                if (obj.name.Contains("Rock") || obj.name.Contains("Stone"))
                                {
                                    obj.transform.SetParent(stonesParent.transform);
                                    stoneCount++;
                                }
                                else
                                {
                                    obj.transform.SetParent(treesParent.transform);
                                    treeCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Direct procedural mesh generation
                        if (_includeTrees)
                        {
                            int parkTreesCount = (realTreesPlaced > 25) ? Mathf.Max(treeCountInPark / 2, 4) : treeCountInPark;
                            List<GameObject> parkTrees = TreeBuilder.ScatterTrees(parkCenters[i], parkRadius, parkTreesCount, shader);
                            foreach (var t in parkTrees)
                            {
                                if (IsInsideAnyBuilding(t.transform.position, buildingBounds) ||
                                    WaterBuilder.IsPointInWater(t.transform.position, waterAreas, waterways, 1.2f) ||
                                    RoadSpatialIndex.IsPointOnRoad(t.transform.position, 2.0f))
                                {
                                    Object.DestroyImmediate(t);
                                }
                                else
                                {
                                    t.transform.SetParent(treesParent.transform);
                                    treeCount++;
                                }
                            }
                        }

                        if (_includeStones && (realRocksPlaced == 0 || Random.value < 0.5f))
                        {
                            int rockCountInPark = Mathf.Clamp(treeCountInPark / 5, 2, 8);
                            for (int r = 0; r < rockCountInPark; r++)
                            {
                                float angle = Random.Range(0f, Mathf.PI * 2f);
                                float dist = Mathf.Sqrt(Random.value) * parkRadius * 0.85f;
                                Vector3 rPos = parkCenters[i] + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                                if (!IsInsideAnyBuilding(rPos, buildingBounds) &&
                                    !WaterBuilder.IsPointInWater(rPos, waterAreas, waterways, 0.5f) &&
                                    !RoadSpatialIndex.IsPointOnRoad(rPos, 1.5f))
                                {
                                    GameObject rObj = RockBuilder.Build(rPos, shader, Random.Range(0.6f, 1.4f));
                                    rObj.transform.SetParent(stonesParent.transform);
                                    stoneCount++;
                                }
                            }
                        }
                    }
                }
            }

            // ── 9. STREET TREES ALONG ROADS ──
            // If few real trees were surveyed, ensure roads have natural avenue greenery
            if (_includeTrees && realTreesPlaced < 35)
            {
                foreach (var way in data.Ways)
                {
                    if (!way.HasTag("highway")) continue;
                    string hwType = (way.GetTag("highway") ?? "").ToLower();
                    if (hwType == "motorway" || hwType == "trunk" || hwType == "footway" || hwType == "path" || hwType == "steps") continue;

                    List<Vector3> roadPath = new List<Vector3>();
                    foreach (long nodeId in way.NodeIds)
                    {
                        if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                            roadPath.Add(shifter.GetLocalPosition(node.Latitude, node.Longitude));
                    }

                    for (int i = 0; i < roadPath.Count - 1; i++)
                    {
                        float segLen = Vector3.Distance(roadPath[i], roadPath[i + 1]);
                        if (segLen < 14f) continue;

                        Vector3 dir = (roadPath[i + 1] - roadPath[i]).normalized;
                        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                        int treesAlongSeg = Mathf.FloorToInt(segLen / 20f);
                        for (int t = 0; t < treesAlongSeg; t++)
                        {
                            if (Random.value > 0.5f) continue;

                            float tPos = (t + 0.5f) / Mathf.Max(treesAlongSeg, 1);
                            Vector3 pos = Vector3.Lerp(roadPath[i], roadPath[i + 1], tPos);

                            float side = Random.value > 0.5f ? 1f : -1f;
                            float segRoadWidth = 9.0f * _roadWidthScale;
                            if (way.Tags.ContainsKey("width") && float.TryParse(way.Tags["width"].Replace("m", ""), out float parsedW))
                                segRoadWidth = parsedW * _roadWidthScale;
                            else if (hwType == "primary" || hwType == "secondary") segRoadWidth = 14f * _roadWidthScale;

                            float treeDist = (segRoadWidth * 0.5f) + 2.0f + 1.8f;
                            Vector3 treePos = pos + right * side * (treeDist + Random.Range(0f, 1.2f));

                            if (!IsInsideAnyBuilding(treePos, buildingBounds) &&
                                !WaterBuilder.IsPointInWater(treePos, waterAreas, waterways, 1.5f) &&
                                !RoadSpatialIndex.IsPointOnRoad(treePos, 1.2f))
                            {
                                GameObject tree = hasTreePrefabs
                                    ? TreeBuilder.BuildPrefab(treePos, _cityController.TreePrefabs, Random.Range(0.5f, 1.0f))
                                    : TreeBuilder.Build(treePos, shader, Random.Range(0.6f, 1.0f));

                                if (tree != null)
                                {
                                    tree.transform.SetParent(treesParent.transform);
                                    treeCount++;
                                }
                            }
                        }
                    }
                }
            }

            // 10. Generate raised platform base
            GameObject ground = GroundBuilder.Build(_radius, groundMat, platformMat);
            ground.transform.SetParent(cityRoot.transform);

            // 11. Street lights along roads
            int streetLightCount = 0;
            GameObject lightsParent = new GameObject("StreetLights");
            lightsParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(lightsParent, "Props");

            foreach (var way in data.Ways)
            {
                if (!way.HasTag("highway")) continue;
                string hwType = (way.GetTag("highway") ?? "").ToLower();
                if (hwType == "footway" || hwType == "path" || hwType == "steps" || hwType == "cycleway") continue;

                List<Vector3> roadPath = new List<Vector3>();
                foreach (long nodeId in way.NodeIds)
                {
                    if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                        roadPath.Add(shifter.GetLocalPosition(node.Latitude, node.Longitude));
                }

                if (_includeSignals)
                {
                    List<GameObject> lights;
                    if (hasLightPrefabs)
                        lights = StreetFurnitureBuilder.PlaceStreetLightPrefabs(roadPath, _cityController.StreetLightPrefabs, 25f);
                    else
                        lights = StreetFurnitureBuilder.PlaceStreetLights(roadPath, shader, 25f);

                    foreach (var light in lights)
                    {
                        if (!IsInsideAnyBuilding(light.transform.position, buildingBounds) &&
                            !WaterBuilder.IsPointInWater(light.transform.position, waterAreas, waterways, 0.5f))
                        {
                            light.transform.SetParent(lightsParent.transform);
                            streetLightCount++;
                        }
                        else
                        {
                            Object.DestroyImmediate(light);
                        }
                    }
                }

                // 11b. Street props along this road
                if (hasPropPrefabs)
                {
                    List<GameObject> props = StreetFurnitureBuilder.PlaceStreetProps(
                        roadPath, _cityController.StreetPropPrefabs, 40f);
                    foreach (var prop in props)
                    {
                        if (!IsInsideAnyBuilding(prop.transform.position, buildingBounds) &&
                            !WaterBuilder.IsPointInWater(prop.transform.position, waterAreas, waterways, 0.5f))
                        {
                            prop.transform.SetParent(propsParent.transform);
                            propCount++;
                        }
                        else
                        {
                            Object.DestroyImmediate(prop);
                        }
                    }
                }

                // 11c. Parked vehicles along this road
                if (_includeVehicles && hasVehiclePrefabs)
                {
                    List<GameObject> cars = VehicleBuilder.PlaceParkedVehicles(
                        roadPath, _cityController.VehiclePrefabs, 30f);
                    foreach (var car in cars)
                    {
                        if (!IsInsideAnyBuilding(car.transform.position, buildingBounds) &&
                            !WaterBuilder.IsPointInWater(car.transform.position, waterAreas, waterways, 0.5f))
                        {
                            car.transform.SetParent(vehiclesParent.transform);
                            vehicleCount++;
                        }
                        else
                        {
                            Object.DestroyImmediate(car);
                        }
                    }
                }
            }

            // 12. Traffic signals at intersections
            if (_includeSignals && hasSignalPrefabs && intersectionCenters.Count > 0)
            {
                List<GameObject> signals = StreetFurnitureBuilder.PlaceTrafficSignals(
                    intersectionCenters, _cityController.TrafficSignalPrefabs);
                foreach (var signal in signals)
                {
                    signal.transform.SetParent(signalsParent.transform);
                    signalCount++;
                }
            }

            // 13. Fill empty lots with vegetation (excluding buildings, roads, and all water areas/corridors)
            if (_includeTrees || _includeStones || _includeGrass)
            {
                Bounds cityBounds = new Bounds(Vector3.zero, Vector3.one * _radius * 2f);
                if (hasTreePrefabs || hasBushPrefabs || hasRockPrefabs || hasGrassPrefabs)
                {
                    GameObject[] lotTrees = _includeTrees ? _cityController.TreePrefabs : null;
                    GameObject[] lotBushes = _includeTrees ? _cityController.BushPrefabs : null;
                    GameObject[] lotRocks = _includeStones ? _cityController.RockPrefabs : null;
                    GameObject[] lotGrass = _includeGrass ? _cityController.GrassPrefabs : null;

                    lotFillCount = LotFiller.FillEmptyLots(
                        cityBounds, buildingBounds, roadBounds,
                        lotTrees,
                        lotBushes,
                        lotRocks,
                        lotFillParent.transform,
                        8f,
                        waterAreas,
                        waterways,
                        lotGrass,
                        grassParent.transform,
                        shader);
                }
                else
                {
                    lotFillCount = LotFiller.FillEmptyLotsProcedural(
                        cityBounds, buildingBounds, roadBounds,
                        shader,
                        lotFillParent.transform,
                        _includeTrees,
                        _includeStones,
                        _includeGrass,
                        grassParent.transform,
                        10f,
                        waterAreas,
                        waterways);
                }
                Debug.Log($"Lot Fill: placed {lotFillCount} vegetation & grass objects in empty spaces.");
            }

            // 13b. 3D Grass across all open green parts (ground platform, riverbanks, lawns)
            if (_includeGrass)
            {
                bool useGrassPrefabs = hasGrassPrefabs && _natureMode == NatureMode.Prefab;
                int groundGrassCount = GrassBuilder.ScatterGroundGreenery(
                    grassParent.transform,
                    _radius,
                    buildingBounds,
                    roadBounds,
                    waterAreas,
                    waterways,
                    beachBounds,
                    useGrassPrefabs ? _cityController.GrassPrefabs : null,
                    shader,
                    2.8f);
                grassCount += groundGrassCount;
                Debug.Log($"CityGenerator: Planted {groundGrassCount} 3D grass clumps across all green terrain & riverbanks.");
            }

            // 14. Scene atmosphere (lighting, fog, post-processing)
            SceneSetup.Setup(_radius);

            // 15. Optimization: Combine meshes by material (skip buildings — LODGroup needs individual renderers)
            // Buildings have LODGroup so we don't combine them
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(roadsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(intersectionsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(parksParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(waterParent);
            if (waterParent != null)
            {
                // Static calm water: destroy any legacy animators
                WaterAnimator[] existingAnims = waterParent.GetComponentsInChildren<WaterAnimator>(true);
                foreach (var wa in existingAnims) Object.DestroyImmediate(wa);
            }
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(beachesParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(treesParent);
            if (stonesParent.transform.childCount > 0)
                GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(stonesParent);
            if (grassParent.transform.childCount > 0)
                GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(grassParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(lightsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(vehiclesParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(propsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(signalsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(lotFillParent);

            // 16. Enhanced Occlusion Culling + Static Batching
            // Buildings: full occlusion (occluder + occludee)
            SetStaticFlags(buildingsParent, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            SetStaticFlags(treesParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(stonesParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(grassParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(lightsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(vehiclesParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(propsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(signalsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(lotFillParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(roadsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(intersectionsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(parksParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(waterParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(beachesParent, StaticEditorFlags.OccludeeStatic);

            Debug.Log($"Generation Complete! Buildings: {buildingCount}, Roads: {roadCount}, Intersections: {intersectionCount}, Parks: {parkCount}, Water: {waterCount}, Beaches: {beachCount}, Trees: {treeCount}, Stones: {stoneCount}, Grass: {grassCount}, StreetLights: {streetLightCount}, Vehicles: {vehicleCount}, Props: {propCount}, TrafficSignals: {signalCount}, LotFill: {lotFillCount}");
            _isGenerating = false;
        }

        private void SetStaticRecursive(GameObject go)
        {
            if (go == null) return;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Transform child in go.transform)
            {
                SetStaticRecursive(child.gameObject);
            }
        }

        /// <summary>
        /// Applies specific static editor flags to a parent and all its children recursively.
        /// Used for differentiated occlusion culling (e.g., buildings as occluders, trees as occludees only).
        /// </summary>
        private void SetStaticFlags(GameObject go, StaticEditorFlags flags)
        {
            if (go == null) return;
            StaticEditorFlags effectiveFlags = flags;
            if (go.GetComponent<LODGroup>() != null || go.GetComponentInParent<LODGroup>() != null)
            {
                effectiveFlags &= ~StaticEditorFlags.BatchingStatic;
            }
            GameObjectUtility.SetStaticEditorFlags(go, effectiveFlags);
            foreach (Transform child in go.transform)
            {
                SetStaticFlags(child.gameObject, flags);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SIZE-AWARE BUILDING PREFAB SELECTION
        // ═══════════════════════════════════════════════════════════

        // Tall buildings: skyscrapers, residential blocks, large shops
        private static readonly string[] TallBuildingNames = new[] {
            "Sky_big", "Sky_small", "Residential", "Super Market"
        };
        // Everything else is considered small (houses, shops, restaurants, etc.)

        private GameObject PickBuildingPrefab(GameObject[] allPrefabs, float footprintArea)
        {
            if (allPrefabs == null || allPrefabs.Length == 0) return null;

            List<GameObject> small = new List<GameObject>();
            List<GameObject> tall = new List<GameObject>();

            foreach (var prefab in allPrefabs)
            {
                if (prefab == null) continue;
                string name = prefab.name;
                bool isTall = false;

                foreach (string pattern in TallBuildingNames)
                {
                    if (name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isTall = true;
                        break;
                    }
                }

                if (isTall) tall.Add(prefab);
                else small.Add(prefab);
            }

            // Pick based on footprint area:
            // < 200 m² → small buildings (houses, small shops)
            // >= 200 m² → tall buildings (skyscrapers, residential, super market)
            List<GameObject> pool;
            if (footprintArea >= 200f && tall.Count > 0)
                pool = tall;
            else if (small.Count > 0)
                pool = small;
            else
            {
                pool = new List<GameObject>();
                foreach (var p in allPrefabs) if (p != null) pool.Add(p);
            }

            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        private class BuildingCandidateInfo
        {
            public OsmWay Way;
            public bool IsPart;
            public List<Vector3> Footprint;
            public Vector3 Centroid;
            public float Area;
            public Bounds Bounds;
            public float MinHeight;
            public float MaxHeight;
        }

        /// <summary>
        /// Applies the OpenStreetMap Simple 3D Buildings specification:
        /// 1. Outer building=* envelope outlines that contain building:part=* features are treated
        ///    as 2D boundary outlines only and are NOT extruded as solid buildings.
        /// 2. Member ways of multipolygon relations and relation outline ways are skipped.
        /// 3. Concentric/duplicate footprints (&lt; 2m centroid distance, similar area) are deduplicated,
        ///    while preserving vertically stacked tiers (different min_height).
        /// 4. Outer campus perimeters enclosing multiple standalone buildings are suppressed.
        /// </summary>
        private HashSet<long> ResolveAndDeduplicateBuildings(OsmData data, OriginShifter shifter)
        {
            List<BuildingCandidateInfo> parts = new List<BuildingCandidateInfo>();
            List<BuildingCandidateInfo> outlines = new List<BuildingCandidateInfo>();

            foreach (var way in data.Ways)
            {
                bool hasBuilding = way.HasTag("building") && (way.GetTag("building") ?? "").ToLower() != "no";
                bool hasPart = way.HasTag("building:part") && (way.GetTag("building:part") ?? "").ToLower() != "no";
                if (!hasBuilding && !hasPart) continue;

                // Skip member ways of multipolygon relations and relation outline ways
                if (way.HasTag("_multipolygon_member") || way.HasTag("_building_outline")) continue;

                List<Vector3> footprint = BuildingBuilder.ExtractFootprint(way, data, shifter);
                if (footprint == null || footprint.Count < 3) continue;

                float area = Mathf.Abs(BuildingBuilder.PolygonArea(footprint));
                if (area < 4f) continue;

                Vector3 centroid = BuildingBuilder.ComputeCentroid(footprint);
                Bounds bounds = new Bounds(centroid, Vector3.zero);
                for (int i = 0; i < footprint.Count; i++)
                    bounds.Encapsulate(footprint[i]);

                float minH = BuildingBuilder.DetermineMinHeight(way);
                float totalH = BuildingBuilder.DetermineHeight(way, area);
                float maxH = Mathf.Max(minH + 3f, totalH);

                var info = new BuildingCandidateInfo
                {
                    Way = way,
                    IsPart = hasPart,
                    Footprint = footprint,
                    Centroid = centroid,
                    Area = area,
                    Bounds = bounds,
                    MinHeight = minH,
                    MaxHeight = maxH
                };

                if (hasPart)
                    parts.Add(info);
                else
                    outlines.Add(info);
            }

            // RULE 1: Filter out 2D building outlines that contain 3D building:part elements
            // (Standard OSM 3D Buildings specification: outer outline is an envelope only)
            List<BuildingCandidateInfo> activeCandidates = new List<BuildingCandidateInfo>(parts);

            foreach (var outline in outlines)
            {
                bool isEnvelopeForParts = false;
                foreach (var part in parts)
                {
                    // Broad phase
                    if (!outline.Bounds.Intersects(part.Bounds)) continue;

                    // Narrow phase 1: Part centroid inside outline polygon
                    if (GeometryUtils.PointInPolygon(part.Centroid.x, part.Centroid.z, outline.Footprint))
                    {
                        isEnvelopeForParts = true;
                        break;
                    }

                    // Narrow phase 2: Centroids close and overlapping bounding box
                    if (Vector3.Distance(outline.Centroid, part.Centroid) < 3.5f)
                    {
                        isEnvelopeForParts = true;
                        break;
                    }

                    // Narrow phase 3: Sample part vertices inside outline
                    int insideCount = 0;
                    for (int i = 0; i < part.Footprint.Count; i++)
                    {
                        if (GeometryUtils.PointInPolygon(part.Footprint[i].x, part.Footprint[i].z, outline.Footprint))
                            insideCount++;
                    }
                    if (insideCount > part.Footprint.Count / 2)
                    {
                        isEnvelopeForParts = true;
                        break;
                    }
                }

                if (!isEnvelopeForParts)
                {
                    activeCandidates.Add(outline);
                }
            }

            // RULE 2: Filter out large campus/plot perimeters enclosing multiple separate building outlines
            bool[] suppressed = new bool[activeCandidates.Count];
            for (int i = 0; i < activeCandidates.Count; i++)
            {
                if (suppressed[i]) continue;
                var a = activeCandidates[i];
                if (a.IsPart) continue;

                int enclosedCount = 0;
                for (int j = 0; j < activeCandidates.Count; j++)
                {
                    if (i == j || suppressed[j]) continue;
                    var b = activeCandidates[j];
                    if (a.Area > b.Area * 1.5f && a.Bounds.Intersects(b.Bounds))
                    {
                        if (GeometryUtils.PointInPolygon(b.Centroid.x, b.Centroid.z, a.Footprint))
                        {
                            enclosedCount++;
                            if (enclosedCount >= 2)
                            {
                                suppressed[i] = true;
                                break;
                            }
                        }
                    }
                }
            }

            // RULE 3: Deduplicate overlapping/concentric footprints
            // If two candidates share almost identical centroids and area, and overlap vertically:
            HashSet<long> resolvedWayIds = new HashSet<long>();

            for (int i = 0; i < activeCandidates.Count; i++)
            {
                if (suppressed[i]) continue;
                var a = activeCandidates[i];

                for (int j = i + 1; j < activeCandidates.Count; j++)
                {
                    if (suppressed[j]) continue;
                    var b = activeCandidates[j];

                    // Check horizontal proximity
                    float dist = Vector3.Distance(a.Centroid, b.Centroid);
                    if (dist > 2.0f) continue;

                    // Check area similarity
                    float maxArea = Mathf.Max(a.Area, b.Area);
                    float areaDiff = Mathf.Abs(a.Area - b.Area) / maxArea;
                    if (areaDiff > 0.35f) continue;

                    // Check vertical overlap:
                    // If one is stacked on top of the other (e.g. minHeight of B >= maxHeight of A - 1f),
                    // they are separate vertical tiers/floors and should NOT be suppressed!
                    bool verticallySeparated = (b.MinHeight >= a.MaxHeight - 1f) || (a.MinHeight >= b.MaxHeight - 1f);
                    if (verticallySeparated) continue;

                    // Duplicate detected! Decide which one to keep
                    // Prefer building:part over outline, or prefer the one with explicit height tags
                    if (b.IsPart && !a.IsPart)
                    {
                        suppressed[i] = true;
                        break;
                    }
                    else
                    {
                        suppressed[j] = true;
                    }
                }

                if (!suppressed[i])
                {
                    resolvedWayIds.Add(a.Way.Id);
                }
            }

            return resolvedWayIds;
        }

        private bool IsInsideAnyBuilding(Vector3 pos, List<Bounds> buildingBounds)
        {
            Vector3 testPos = new Vector3(pos.x, 5f, pos.z);
            foreach (var b in buildingBounds)
            {
                if (b.Contains(testPos))
                    return true;
            }
            return false;
        }

        private bool IsArea(OsmWay way, string areaType)
        {
            string landuse = (way.GetTag("landuse") ?? "").ToLower();
            string leisure = (way.GetTag("leisure") ?? "").ToLower();
            string natural = (way.GetTag("natural") ?? "").ToLower();
            return landuse == areaType || leisure == areaType || natural == areaType;
        }

        private bool IsBeachArea(OsmWay way)
        {
            string natural = (way.GetTag("natural") ?? "").ToLower();
            return natural == "beach" || natural == "sand";
        }

        private bool IsWaterArea(OsmWay way)
        {
            string natural = (way.GetTag("natural") ?? "").ToLower();
            string waterway = (way.GetTag("waterway") ?? "").ToLower();
            string water = (way.GetTag("water") ?? "").ToLower();
            string landuse = (way.GetTag("landuse") ?? "").ToLower();
            string relType = (way.GetTag("type") ?? "").ToLower();

            // type=waterway from relation assembly = water
            if (relType == "waterway") return true;

            return natural == "water" || natural == "bay" || natural == "wetland"
                || natural == "coastline"
                || waterway == "riverbank" || waterway == "dock" || waterway == "boatyard"
                || waterway == "river" || waterway == "canal"  // Area river/canal polygons
                || water == "lake" || water == "river" || water == "reservoir"
                || water == "pond" || water == "basin" || water == "lagoon"
                || water == "oxbow" || water == "canal" || water == "reflecting_pool"
                || landuse == "reservoir" || landuse == "basin";
        }

        private bool IsLinearWaterway(OsmWay way)
        {
            string waterway = (way.GetTag("waterway") ?? "").ToLower();
            if (waterway != "river" && waterway != "stream" && waterway != "canal"
                && waterway != "drain" && waterway != "ditch")
                return false;

            // Only treat as linear if the way is NOT a closed polygon
            // (closed waterway polygons are handled as areas by IsWaterArea)
            var nodes = way.NodeIds;
            if (nodes.Count >= 3 && nodes[0] == nodes[nodes.Count - 1])
                return false; // Closed ring = area, not linear

            return true;
        }

        private float DetermineRiverWidth(OsmWay way)
        {
            string waterway = (way.GetTag("waterway") ?? "").ToLower();
            switch (waterway)
            {
                case "river": return 20f;
                case "canal": return 12f;
                case "stream": return 5f;
                case "drain": return 3f;
                case "ditch": return 2f;
                default: return 8f;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // ═══════════════════════════════════════════════════════════
        //  INTERSECTION FILL GENERATION
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Generates seamless asphalt road junction caps and perimeter sidewalk corner curbs.
        /// </summary>
        private int GenerateIntersections(
            Transform parent,
            List<OsmWay> highwayWays,
            OsmData data,
            OriginShifter shifter,
            Dictionary<string, Material> roadMaterials,
            Material defaultRoadMat,
            Material defaultSidewalkMat,
            out List<Vector3> intersectionCenters)
        {
            intersectionCenters = new List<Vector3>();
            var junctions = IntersectionBuilder.DetectIntersections(
                highwayWays, data, shifter, roadMaterials, defaultRoadMat, defaultSidewalkMat, _roadWidthScale);

            int intersectionCount = 0;
            for (int i = 0; i < junctions.Count; i++)
            {
                var junc = junctions[i];
                intersectionCenters.Add(junc.Center);
                GameObject juncObj = IntersectionBuilder.BuildJunction(junc, intersectionCount, parent);
                if (juncObj != null) intersectionCount++;
            }

            return intersectionCount;
        }

        private GameObject CreatePolygonMesh(List<Vector3> points, Material material, string name)
        {
            if (points.Count < 3) return null;

            // Remove very close duplicate points which can break triangulation
            List<Vector3> cleanPoints = new List<Vector3>();
            foreach (var p in points)
            {
                bool isDup = false;
                foreach (var cp in cleanPoints)
                {
                    if (Vector3.Distance(p, cp) < 0.1f)
                    {
                        isDup = true;
                        break;
                    }
                }
                if (!isDup) cleanPoints.Add(p);
            }

            if (cleanPoints.Count < 3) return null;

            List<int> tris = GeoCity3D.Geometry.GeometryUtils.Triangulate(cleanPoints);
            if (tris == null || tris.Count < 3) return null;

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            mesh.SetVertices(cleanPoints);
            mesh.SetTriangles(tris, 0);

            // Planar UV projection based on world space (tiling handled by material)
            Vector2[] uvs = new Vector2[cleanPoints.Count];
            for (int i = 0; i < cleanPoints.Count; i++)
            {
                uvs[i] = new Vector2(cleanPoints[i].x * 0.1f, cleanPoints[i].z * 0.1f);
            }
            mesh.SetUVs(0, uvs);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            return go;
        }

        // ═══════════════════════════════════════════════════════════
        //  SYNTHETIC PROCEDURAL MAP GENERATOR
        // ═══════════════════════════════════════════════════════════

        private IEnumerator GenerateProceduralMap()
        {
            Debug.Log("Starting Procedural Map Generation...");
            EnsureCityController();
            SanitizeController(_cityController);
            float radius = _proceduralMapRadius;

            // 1. Setup Origin
            var shifter = FindFirstObjectByType<OriginShifter>();
            if (shifter == null)
            {
                GameObject shifterObj = new GameObject("OriginShifter");
                shifter = shifterObj.AddComponent<OriginShifter>();
            }
            shifter.SetOrigin(0.0, 0.0);

            // 2. Find shader
            Shader shader = FindBestShader();
            if (shader == null)
            {
                Debug.LogError("No valid shader found!");
                _isGenerating = false;
                yield break;
            }

            GrassBuilder.ResetMaterialPool();

            // 3. Materials
            Texture2D facadeNormalMap = TextureGenerator.CreateFacadeNormalMap();
            yield return null;
            Material buildingMat = _cityController.BuildingWallMaterial != null
                ? _cityController.BuildingWallMaterial
                : CreateTexturedMaterial(shader, TextureGenerator.CreateFacadeTexture(512, 512, new Color(0.92f, 0.91f, 0.89f)), facadeNormalMap, 0.25f);
            yield return null;
            Material roofMat = _cityController.BuildingRoofMaterial != null
                ? _cityController.BuildingRoofMaterial
                : CreateTexturedMaterial(shader, TextureGenerator.CreateRoofTexture(256, 256, new Color(0.38f, 0.38f, 0.40f)), 0.15f);
            yield return null;
            Material windowMat = _cityController.BuildingWindowMaterial != null
                ? _cityController.BuildingWindowMaterial
                : CreateGlassMaterial(shader);
            yield return null;

            Texture2D roadNormalMap = TextureGenerator.CreateAsphaltNormalMap();
            yield return null;
            Material primaryRoadMat = _cityController.PrimaryRoadMaterial != null ? _cityController.PrimaryRoadMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreatePrimaryRoadTexture(), roadNormalMap, 0.05f);
            Material residentialRoadMat = _cityController.ResidentialRoadMaterial != null ? _cityController.ResidentialRoadMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateResidentialRoadTexture(), roadNormalMap, 0.05f);
            Material sidewalkMat = _cityController.SidewalkMaterial != null ? _cityController.SidewalkMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateSidewalkTexture(), 0.1f);
            Material parkMat = _cityController.ParkMaterial != null ? _cityController.ParkMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateParkTexture(), 0.05f);
            Material waterMat = _cityController.WaterMaterial != null ? _cityController.WaterMaterial : CreateWaterMaterial(shader);
            Material groundMat = _cityController.GroundMaterial != null ? _cityController.GroundMaterial : CreateTexturedMaterial(shader, TextureGenerator.CreateGroundTexture(), 0.1f);
            Material platformMat = CreateSolidMaterial(shader, new Color(0.28f, 0.28f, 0.30f), 0.15f);

            // 4. Root hierarchy
            GameObject cityRoot = new GameObject("ProceduralCity");
            cityRoot.transform.position = Vector3.zero;

            GameObject ground = GroundBuilder.Build(radius, groundMat, platformMat);
            ground.transform.SetParent(cityRoot.transform);

            GameObject waterParent = new GameObject("Water");
            waterParent.transform.SetParent(cityRoot.transform);

            GameObject roadsParent = new GameObject("Roads");
            roadsParent.transform.SetParent(cityRoot.transform);

            GameObject buildingsParent = new GameObject("Buildings");
            buildingsParent.transform.SetParent(cityRoot.transform);

            GameObject parksParent = new GameObject("Parks");
            parksParent.transform.SetParent(cityRoot.transform);

            GameObject treesParent = new GameObject("Trees");
            treesParent.transform.SetParent(cityRoot.transform);

            GameObject stonesParent = new GameObject("Stones");
            stonesParent.transform.SetParent(cityRoot.transform);

            GameObject grassParent = new GameObject("Grass");
            grassParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(grassParent, "Grass");

            GameObject vehiclesParent = new GameObject("Vehicles");
            vehiclesParent.transform.SetParent(cityRoot.transform);

            GameObject lightsParent = new GameObject("StreetLights");
            lightsParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(lightsParent, "Props");

            GameObject signalsParent = new GameObject("TrafficSignals");
            signalsParent.transform.SetParent(cityRoot.transform);
            AssignLayerIfFound(signalsParent, "Props");

            RoadBuilder.ClearIntersectionData();
            StreetFurnitureBuilder.ResetMaterialPool();
            TreeBuilder.ResetMaterialPool();
            RockBuilder.ResetMaterialPool();
            GrassBuilder.ResetMaterialPool();
            LODBuilder.ResetPalette();
            _sharedCarMaterials = null;
            _proceduralRockMat = null;
            _sharedSignalPoleMat = null;

            int buildingCount = 0;
            int roadCount = 0;
            int waterCount = 0;
            int treeCount = 0;
            int stoneCount = 0;
            int grassCount = 0;
            int vehicleCount = 0;
            int lightCount = 0;
            int signalCount = 0;

            // ── 5. RIVER GENERATION ──
            float riverWidth = 24f;
            List<Vector3> riverPath = new List<Vector3>();
            if (_includeRiver)
            {
                float riverMinX = -radius * 1.05f;
                float riverMaxX = radius * 1.05f;
                int riverSteps = 16;
                for (int i = 0; i <= riverSteps; i++)
                {
                    float t = (float)i / riverSteps;
                    float rx = Mathf.Lerp(riverMinX, riverMaxX, t);
                    float rz = Mathf.Sin(t * Mathf.PI * 2.2f) * (radius * 0.28f) + (t - 0.5f) * (radius * 0.15f);
                    riverPath.Add(new Vector3(rx, 0f, rz));
                }

                GameObject river = WaterBuilder.BuildRiver(riverPath, riverWidth, waterMat, 9001, "Procedural_River");
                if (river != null)
                {
                    river.transform.SetParent(waterParent.transform);
                    waterCount++;
                }
            }

            // ── 6. LAKE GENERATION ──
            Vector3 lakeCenter = new Vector3(radius * 0.42f, 0f, radius * 0.42f);
            float lakeBaseRadius = radius * 0.20f;
            List<Vector3> lakePolygon = new List<Vector3>();
            List<Vector3> lakeParkPoly = new List<Vector3>();
            if (_includeLake)
            {
                int lakePoints = 20;
                for (int i = 0; i < lakePoints; i++)
                {
                    float angle = (float)i / lakePoints * Mathf.PI * 2f;
                    float r = lakeBaseRadius + Mathf.Sin(angle * 3f) * (lakeBaseRadius * 0.25f) + Mathf.Cos(angle * 5f) * (lakeBaseRadius * 0.12f);
                    float lx = lakeCenter.x + Mathf.Cos(angle) * r;
                    float lz = lakeCenter.z + Mathf.Sin(angle) * r;
                    lakePolygon.Add(new Vector3(lx, 0f, lz));
                }

                GameObject lake = WaterBuilder.BuildLake(lakePolygon, waterMat, 9002, "Procedural_Lake");
                if (lake != null)
                {
                    lake.transform.SetParent(waterParent.transform);
                    waterCount++;
                }

                // Park surrounding lake
                for (int i = 0; i < lakePoints; i++)
                {
                    float angle = (float)i / lakePoints * Mathf.PI * 2f;
                    float r = (lakeBaseRadius + Mathf.Sin(angle * 3f) * (lakeBaseRadius * 0.25f) + Mathf.Cos(angle * 5f) * (lakeBaseRadius * 0.12f)) * 1.30f;
                    lakeParkPoly.Add(new Vector3(lakeCenter.x + Mathf.Cos(angle) * r, 0f, lakeCenter.z + Mathf.Sin(angle) * r));
                }
                GameObject lakePark = AreaBuilder.Build(lakeParkPoly, parkMat, 9003, "LakePark", 0.02f);
                if (lakePark != null) lakePark.transform.SetParent(parksParent.transform);
            }

            // Water reference lists for exclusion checks
            List<WaterAreaInfo> procWaterAreas = new List<WaterAreaInfo>();
            List<WaterwayInfo> procWaterways = new List<WaterwayInfo>();
            if (_includeRiver && riverPath != null && riverPath.Count >= 2)
                procWaterways.Add(new WaterwayInfo(riverPath, riverWidth));
            if (_includeLake && lakePolygon != null && lakePolygon.Count >= 3)
                procWaterAreas.Add(new WaterAreaInfo(lakePolygon));

            // ── 7. ROAD NETWORK & BRIDGES ──
            float gridSpacing = 80f;
            List<float> xRoads = new List<float>();
            List<float> zRoads = new List<float>();

            for (float x = -radius * 0.72f; x <= radius * 0.72f; x += gridSpacing)
                xRoads.Add(x);
            for (float z = -radius * 0.72f; z <= radius * 0.72f; z += gridSpacing)
                zRoads.Add(z);

            List<List<Vector3>> allRoadPaths = new List<List<Vector3>>();
            HashSet<List<Vector3>> bridgeRoadPaths = new HashSet<List<Vector3>>();

            // North-South Roads & Bridges
            foreach (float rx in xRoads)
            {
                if (_includeRiver && riverPath.Count >= 2)
                {
                    float tRoad = (rx - (-radius * 1.05f)) / (2.1f * radius);
                    float riverZAtRx = Mathf.Sin(tRoad * Mathf.PI * 2.2f) * (radius * 0.28f) + (tRoad - 0.5f) * (radius * 0.15f);
                    // 26m approach runway on solid land on each riverbank for a realistic, smooth ~14% grade climb
                    float bridgeHalfSpan = riverWidth * 0.5f + 26f;

                    List<Vector3> southRoad = new List<Vector3>
                    {
                        new Vector3(rx, 0f, -radius * 0.75f),
                        new Vector3(rx, 0f, riverZAtRx - bridgeHalfSpan)
                    };
                    List<Vector3> bridgePath = new List<Vector3>
                    {
                        new Vector3(rx, 0f, riverZAtRx - bridgeHalfSpan),
                        new Vector3(rx, 0f, riverZAtRx),
                        new Vector3(rx, 0f, riverZAtRx + bridgeHalfSpan)
                    };
                    List<Vector3> northRoad = new List<Vector3>
                    {
                        new Vector3(rx, 0f, riverZAtRx + bridgeHalfSpan),
                        new Vector3(rx, 0f, radius * 0.75f)
                    };

                    GameObject r1 = RoadBuilder.CreateSolidStrip(southRoad, 8f, 0.08f, 0.12f, primaryRoadMat, $"Road_S_{rx}");
                    if (r1 != null) { r1.transform.SetParent(roadsParent.transform); roadCount++; allRoadPaths.Add(southRoad); }

                    GameObject br = BridgeBuilder.Build(bridgePath, 8f, primaryRoadMat, sidewalkMat, (long)(rx + 100000), "primary");
                    if (br != null)
                    {
                        br.transform.SetParent(roadsParent.transform);
                        roadCount++;
                        allRoadPaths.Add(bridgePath);
                        bridgeRoadPaths.Add(bridgePath);
                    }

                    GameObject r2 = RoadBuilder.CreateSolidStrip(northRoad, 8f, 0.08f, 0.12f, primaryRoadMat, $"Road_N_{rx}");
                    if (r2 != null) { r2.transform.SetParent(roadsParent.transform); roadCount++; allRoadPaths.Add(northRoad); }
                }
                else
                {
                    List<Vector3> roadPath = new List<Vector3>
                    {
                        new Vector3(rx, 0f, -radius * 0.75f),
                        new Vector3(rx, 0f, radius * 0.75f)
                    };
                    GameObject r = RoadBuilder.CreateSolidStrip(roadPath, 8f, 0.08f, 0.12f, primaryRoadMat, $"Road_NS_{rx}");
                    if (r != null) { r.transform.SetParent(roadsParent.transform); roadCount++; allRoadPaths.Add(roadPath); }
                }
            }

            // East-West Roads
            foreach (float rz in zRoads)
            {
                if (_includeLake && Mathf.Abs(rz - lakeCenter.z) < lakeBaseRadius * 0.85f)
                    continue; // Don't cut through the lake

                List<Vector3> roadPath = new List<Vector3>
                {
                    new Vector3(-radius * 0.75f, 0f, rz),
                    new Vector3(radius * 0.75f, 0f, rz)
                };
                GameObject r = RoadBuilder.CreateSolidStrip(roadPath, 7f, 0.08f, 0.12f, residentialRoadMat, $"Road_EW_{rz}");
                if (r != null) { r.transform.SetParent(roadsParent.transform); roadCount++; allRoadPaths.Add(roadPath); }
            }

            // Collect intersections for signal placement
            List<Vector3> intersectionCenters = new List<Vector3>();
            foreach (float rx in xRoads)
            {
                foreach (float rz in zRoads)
                {
                    Vector3 intPt = new Vector3(rx, 0f, rz);
                    if (_includeLake && Vector3.Distance(intPt, lakeCenter) < lakeBaseRadius * 1.15f)
                        continue;
                    if (_includeRiver && WaterBuilder.IsPointInWater(intPt, procWaterAreas, procWaterways, 4f))
                        continue;
                    intersectionCenters.Add(intPt);
                }
            }

            yield return null;

            // ── 8. IDENTICAL ROUNDED PROCEDURAL BUILDINGS ──
            List<Bounds> buildingBounds = new List<Bounds>();
            float buildingWidth = 22f;
            float buildingDepth = 22f;
            float bCornerRadius = _buildingCornerRadius;
            int bId = 1;

            for (int xi = 0; xi < xRoads.Count - 1; xi++)
            {
                float x1 = xRoads[xi] + 8f;
                float x2 = xRoads[xi + 1] - 8f;

                for (int zi = 0; zi < zRoads.Count - 1; zi++)
                {
                    float z1 = zRoads[zi] + 8f;
                    float z2 = zRoads[zi + 1] - 8f;

                    float blockW = x2 - x1;
                    float blockH = z2 - z1;
                    if (blockW < buildingWidth || blockH < buildingDepth) continue;

                    int countX = Mathf.FloorToInt((blockW + 4f) / (buildingWidth + 6f));
                    int countZ = Mathf.FloorToInt((blockH + 4f) / (buildingDepth + 6f));
                    if (countX < 1 || countZ < 1) continue;

                    float stepX = blockW / countX;
                    float stepZ = blockH / countZ;

                    for (int bx = 0; bx < countX; bx++)
                    {
                        for (int bz = 0; bz < countZ; bz++)
                        {
                            float cx = x1 + (bx + 0.5f) * stepX;
                            float cz = z1 + (bz + 0.5f) * stepZ;
                            Vector3 pos = new Vector3(cx, 0f, cz);

                            // Distance checks against lake and river
                            if (_includeLake && Vector3.Distance(pos, lakeCenter) < lakeBaseRadius * 1.35f)
                                continue;

                            if (WaterBuilder.IsPointInWater(pos, procWaterAreas, procWaterways, 12f))
                                continue;

                            float halfW = buildingWidth * 0.5f;
                            float halfD = buildingDepth * 0.5f;
                            List<Vector3> footprint = new List<Vector3>
                            {
                                new Vector3(cx - halfW, 0f, cz - halfD),
                                new Vector3(cx + halfW, 0f, cz - halfD),
                                new Vector3(cx + halfW, 0f, cz + halfD),
                                new Vector3(cx - halfW, 0f, cz + halfD)
                            };

                            // Identical 4-floor architectural building height
                            float bHeight = 12.8f;
                            GameObject bObj = BuildingBuilder.BuildFromFootprint(footprint, bHeight,
                                buildingMat, roofMat, bId++, bCornerRadius, windowMat);

                            if (bObj != null)
                            {
                                bObj.transform.SetParent(buildingsParent.transform);
                                LODBuilder.AddLOD(bObj);
                                buildingCount++;

                                Renderer[] renderers = bObj.GetComponentsInChildren<Renderer>();
                                if (renderers.Length > 0)
                                {
                                    Bounds totalBounds = renderers[0].bounds;
                                    for (int i = 1; i < renderers.Length; i++)
                                        totalBounds.Encapsulate(renderers[i].bounds);
                                    buildingBounds.Add(totalBounds);
                                }
                            }
                        }
                    }
                }
            }

            // Determine if CityController has prefabs assigned
            bool hasTreePrefabs = HasValidPrefabs(_cityController.TreePrefabs);
            bool hasRockPrefabs = HasValidPrefabs(_cityController.RockPrefabs);
            bool hasGrassPrefabs = HasValidPrefabs(_cityController.GrassPrefabs);
            bool hasLightPrefabs = HasValidPrefabs(_cityController.StreetLightPrefabs);
            bool hasSignalPrefabs = HasValidPrefabs(_cityController.TrafficSignalPrefabs);
            bool hasVehiclePrefabs = HasValidPrefabs(_cityController.VehiclePrefabs);

            // ── 9. TREES & VEGETATION ──
            if (_includeTrees)
            {
                // Riverbank trees
                if (_includeRiver)
                {
                    for (int i = 0; i < riverPath.Count; i += 2)
                    {
                        Vector3 fwd = i < riverPath.Count - 1 ? (riverPath[i + 1] - riverPath[i]).normalized : Vector3.right;
                        Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                        Vector3 tPosL = riverPath[i] - rgt * (riverWidth * 0.5f + 4.5f);
                        Vector3 tPosR = riverPath[i] + rgt * (riverWidth * 0.5f + 4.5f);
                        if (!WaterBuilder.IsPointInWater(tPosL, procWaterAreas, procWaterways, 1.2f) &&
                            !IsInsideAnyBuilding(tPosL, buildingBounds))
                        {
                            GameObject treeL = hasTreePrefabs
                                ? TreeBuilder.BuildPrefab(tPosL, _cityController.TreePrefabs, Random.Range(0.8f, 1.2f))
                                : TreeBuilder.Build(tPosL, shader, 1.2f);
                            if (treeL != null) { treeL.transform.SetParent(treesParent.transform); treeCount++; }
                        }
                        if (!WaterBuilder.IsPointInWater(tPosR, procWaterAreas, procWaterways, 1.2f) &&
                            !IsInsideAnyBuilding(tPosR, buildingBounds))
                        {
                            GameObject treeR = hasTreePrefabs
                                ? TreeBuilder.BuildPrefab(tPosR, _cityController.TreePrefabs, Random.Range(0.8f, 1.2f))
                                : TreeBuilder.Build(tPosR, shader, 1.2f);
                            if (treeR != null) { treeR.transform.SetParent(treesParent.transform); treeCount++; }
                        }
                    }
                }

                // Lakeside trees
                if (_includeLake)
                {
                    int treeCountOnLake = 16;
                    for (int i = 0; i < treeCountOnLake; i++)
                    {
                        float angle = (float)i / treeCountOnLake * Mathf.PI * 2f;
                        float r = lakeBaseRadius * 1.25f;
                        Vector3 tPos = new Vector3(lakeCenter.x + Mathf.Cos(angle) * r, 0f, lakeCenter.z + Mathf.Sin(angle) * r);
                        if (!WaterBuilder.IsPointInWater(tPos, procWaterAreas, procWaterways, 1.2f) &&
                            !IsInsideAnyBuilding(tPos, buildingBounds))
                        {
                            GameObject tree = hasTreePrefabs
                                ? TreeBuilder.BuildPrefab(tPos, _cityController.TreePrefabs, Random.Range(0.8f, 1.3f))
                                : TreeBuilder.Build(tPos, shader, 1.3f);
                            if (tree != null) { tree.transform.SetParent(treesParent.transform); treeCount++; }
                        }
                    }
                }

                // Avenue trees along roads (skip bridges)
                foreach (var rPath in allRoadPaths)
                {
                    if (bridgeRoadPaths.Contains(rPath)) continue;

                    for (int i = 0; i < rPath.Count - 1; i++)
                    {
                        float segLen = Vector3.Distance(rPath[i], rPath[i + 1]);
                        if (segLen < 16f) continue;
                        Vector3 dir = (rPath[i + 1] - rPath[i]).normalized;
                        Vector3 rgt = Vector3.Cross(Vector3.up, dir).normalized;
                        int countOnSeg = Mathf.FloorToInt(segLen / 24f);
                        for (int s = 0; s < countOnSeg; s++)
                        {
                            float t = (s + 0.5f) / Mathf.Max(countOnSeg, 1);
                            Vector3 pt = Vector3.Lerp(rPath[i], rPath[i + 1], t);
                            float side = (s % 2 == 0) ? 1f : -1f;
                            Vector3 tPos = pt + rgt * (side * 5.5f);
                            if (!WaterBuilder.IsPointInWater(tPos, procWaterAreas, procWaterways, 1.5f) &&
                                !IsInsideAnyBuilding(tPos, buildingBounds))
                            {
                                GameObject tree = hasTreePrefabs
                                    ? TreeBuilder.BuildPrefab(tPos, _cityController.TreePrefabs, Random.Range(0.7f, 1.1f))
                                    : TreeBuilder.Build(tPos, shader, Random.Range(0.7f, 1.1f));
                                if (tree != null) { tree.transform.SetParent(treesParent.transform); treeCount++; }
                            }
                        }
                    }
                }
            }

            // ── 9b. GRASS & GROUND COVER ──
            if (_includeGrass)
            {
                // Lakeside park grass
                if (_includeLake && lakeParkPoly != null && lakeParkPoly.Count >= 3)
                {
                    List<GameObject> lakeGrass = GrassBuilder.ScatterInPolygon(
                        lakeParkPoly, 450,
                        hasGrassPrefabs ? _cityController.GrassPrefabs : null,
                        shader, 0.02f);
                    foreach (var g in lakeGrass)
                    {
                        if (WaterBuilder.IsPointInWater(g.transform.position, procWaterAreas, procWaterways, 0.4f) ||
                            IsInsideAnyBuilding(g.transform.position, buildingBounds))
                        {
                            Object.DestroyImmediate(g);
                        }
                        else
                        {
                            g.transform.SetParent(grassParent.transform);
                            grassCount++;
                        }
                    }
                }

                // Riverbank grass tufts
                if (_includeRiver && riverPath != null && riverPath.Count >= 2)
                {
                    for (int i = 0; i < riverPath.Count; i++)
                    {
                        Vector3 fwd = (i < riverPath.Count - 1) ? (riverPath[i + 1] - riverPath[i]).normalized : Vector3.right;
                        Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float dist = riverWidth * 0.5f + Random.Range(1.0f, 3.8f);
                            Vector3 gPos = riverPath[i] + rgt * (side * dist) + fwd * Random.Range(-2.5f, 2.5f);
                            gPos.y = 0.02f;
                            if (!WaterBuilder.IsPointInWater(gPos, procWaterAreas, procWaterways, 0.4f) &&
                                !IsInsideAnyBuilding(gPos, buildingBounds))
                            {
                                GameObject g = hasGrassPrefabs
                                    ? GrassBuilder.BuildPrefab(gPos, _cityController.GrassPrefabs, Random.Range(0.85f, 1.4f))
                                    : GrassBuilder.BuildProceduralTuft(gPos, shader, Random.Range(0.85f, 1.4f));
                                if (g != null)
                                {
                                    g.transform.SetParent(grassParent.transform);
                                    grassCount++;
                                }
                            }
                        }
                    }
                }

                // Open green terrain 3D grass scattering
                List<Bounds> procRoadBounds = new List<Bounds>();
                Renderer[] rRenderers = roadsParent.GetComponentsInChildren<Renderer>();
                foreach (var rr in rRenderers) procRoadBounds.Add(rr.bounds);

                bool useGrassPrefabs = hasGrassPrefabs && _natureMode == NatureMode.Prefab;
                int procGroundGrass = GrassBuilder.ScatterGroundGreenery(
                    grassParent.transform,
                    radius,
                    buildingBounds,
                    procRoadBounds,
                    procWaterAreas,
                    procWaterways,
                    null,
                    useGrassPrefabs ? _cityController.GrassPrefabs : null,
                    shader,
                    2.8f);
                grassCount += procGroundGrass;
            }

            // ── 10. STONES & ROCKS ──
            if (_includeStones)
            {
                // Riverbank rocks
                if (_includeRiver)
                {
                    for (int i = 0; i < riverPath.Count; i++)
                    {
                        Vector3 fwd = (i < riverPath.Count - 1) ? (riverPath[i + 1] - riverPath[i]).normalized : Vector3.right;
                        Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (Random.value < 0.35f) continue;
                            float dist = riverWidth * 0.5f + Random.Range(1.5f, 4.5f);
                            Vector3 rockPos = riverPath[i] + rgt * (side * dist) + fwd * Random.Range(-3f, 3f);
                            rockPos.y = 0f;
                            if (!WaterBuilder.IsPointInWater(rockPos, procWaterAreas, procWaterways, 0.4f) &&
                                !IsInsideAnyBuilding(rockPos, buildingBounds))
                            {
                                GameObject rock = hasRockPrefabs
                                    ? TreeBuilder.BuildPrefab(rockPos, _cityController.RockPrefabs, Random.Range(0.6f, 1.5f))
                                    : BuildProceduralRock(rockPos, Random.Range(0.7f, 1.6f), shader);
                                if (rock != null) { rock.transform.SetParent(stonesParent.transform); stoneCount++; }
                            }
                        }
                    }
                }

                // Lakeside shoreline rocks
                if (_includeLake)
                {
                    int rockCountOnLake = 18;
                    for (int i = 0; i < rockCountOnLake; i++)
                    {
                        float angle = (float)i / rockCountOnLake * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
                        float r = lakeBaseRadius * Random.Range(1.04f, 1.22f);
                        Vector3 rockPos = new Vector3(lakeCenter.x + Mathf.Cos(angle) * r, 0f, lakeCenter.z + Mathf.Sin(angle) * r);
                        if (!WaterBuilder.IsPointInWater(rockPos, procWaterAreas, procWaterways, 0.4f) &&
                            !IsInsideAnyBuilding(rockPos, buildingBounds))
                        {
                            GameObject rock = hasRockPrefabs
                                ? TreeBuilder.BuildPrefab(rockPos, _cityController.RockPrefabs, Random.Range(0.7f, 1.6f))
                                : BuildProceduralRock(rockPos, Random.Range(0.8f, 1.8f), shader);
                            if (rock != null) { rock.transform.SetParent(stonesParent.transform); stoneCount++; }
                        }
                    }
                }
            }

            // ── 11. VEHICLES (CARS) ──
            if (_includeVehicles)
            {
                foreach (var rPath in allRoadPaths)
                {
                    if (hasVehiclePrefabs)
                    {
                        List<GameObject> cars = VehicleBuilder.PlaceParkedVehicles(
                            rPath, _cityController.VehiclePrefabs, 32f);
                        foreach (var car in cars)
                        {
                            if (!IsInsideAnyBuilding(car.transform.position, buildingBounds) &&
                                !WaterBuilder.IsPointInWater(car.transform.position, procWaterAreas, procWaterways, 0.5f))
                            {
                                car.transform.SetParent(vehiclesParent.transform);
                                vehicleCount++;
                            }
                            else
                            {
                                Object.DestroyImmediate(car);
                            }
                        }
                    }
                    else
                    {
                        vehicleCount += PlaceProceduralVehiclesAlongPath(rPath, shader, vehiclesParent, buildingBounds, procWaterAreas, procWaterways, 32f);
                    }
                }
            }

            // ── 12. SIGNALS & STREET LIGHTS ──
            if (_includeSignals)
            {
                // Street lights along roads
                foreach (var rPath in allRoadPaths)
                {
                    List<GameObject> lights = hasLightPrefabs
                        ? StreetFurnitureBuilder.PlaceStreetLightPrefabs(rPath, _cityController.StreetLightPrefabs, 35f)
                        : StreetFurnitureBuilder.PlaceStreetLights(rPath, shader, 35f);

                    foreach (var light in lights)
                    {
                        if (!IsInsideAnyBuilding(light.transform.position, buildingBounds) &&
                            !WaterBuilder.IsPointInWater(light.transform.position, procWaterAreas, procWaterways, 0.5f))
                        {
                            light.transform.SetParent(lightsParent.transform);
                            lightCount++;
                        }
                        else
                        {
                            Object.DestroyImmediate(light);
                        }
                    }
                }

                // Traffic signals at intersections
                if (hasSignalPrefabs)
                {
                    List<GameObject> signals = StreetFurnitureBuilder.PlaceTrafficSignals(
                        intersectionCenters, _cityController.TrafficSignalPrefabs);
                    foreach (var signal in signals)
                    {
                        if (!IsInsideAnyBuilding(signal.transform.position, buildingBounds) &&
                            !WaterBuilder.IsPointInWater(signal.transform.position, procWaterAreas, procWaterways, 0.5f))
                        {
                            signal.transform.SetParent(signalsParent.transform);
                            signalCount++;
                        }
                        else
                        {
                            Object.DestroyImmediate(signal);
                        }
                    }
                }
                else
                {
                    foreach (var center in intersectionCenters)
                    {
                        Vector3[] cornerOffsets = new Vector3[]
                        {
                            new Vector3(4.8f, 0f, 4.8f),
                            new Vector3(-4.8f, 0f, -4.8f)
                        };
                        foreach (var off in cornerOffsets)
                        {
                            Vector3 sPos = center + off;
                            if (!IsInsideAnyBuilding(sPos, buildingBounds) &&
                                !WaterBuilder.IsPointInWater(sPos, procWaterAreas, procWaterways, 0.5f))
                            {
                                float yAngle = Mathf.Atan2(-off.x, -off.z) * Mathf.Rad2Deg;
                                GameObject signal = BuildProceduralTrafficSignal(sPos, Quaternion.Euler(0f, yAngle, 0f), shader);
                                if (signal != null)
                                {
                                    signal.transform.SetParent(signalsParent.transform);
                                    signalCount++;
                                }
                            }
                        }
                    }
                }
            }

            yield return null;

            // ── 12b. SCENE ATMOSPHERE (Realistic lighting, shadows, sky reflections, aerial fog) ──
            SceneSetup.Setup(radius);

            // ── 13. MESH COMBINING & BATCHING ──
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(roadsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(waterParent);
            if (waterParent != null)
            {
                // Static calm water: remove any animators
                WaterAnimator[] existingAnims = waterParent.GetComponentsInChildren<WaterAnimator>(true);
                foreach (var wa in existingAnims) Object.DestroyImmediate(wa);
            }
            if (parksParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(parksParent);
            if (treesParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(treesParent);
            if (stonesParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(stonesParent);
            if (grassParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(grassParent);
            if (vehiclesParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(vehiclesParent);
            if (lightsParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(lightsParent);
            if (signalsParent.transform.childCount > 0) GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(signalsParent);

            SetStaticFlags(buildingsParent, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(roadsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(waterParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(parksParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(treesParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(stonesParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(grassParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(vehiclesParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(lightsParent, StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(signalsParent, StaticEditorFlags.OccludeeStatic);

            Debug.Log($"Procedural Map Complete! Buildings: {buildingCount}, Roads: {roadCount}, Water Bodies: {waterCount}, Trees: {treeCount}, Stones: {stoneCount}, Grass: {grassCount}, Vehicles: {vehicleCount}, Lights: {lightCount}, Signals: {signalCount}");
            _isGenerating = false;
        }

        // ═══════════════════════════════════════════════════════════
        //  PROCEDURAL FALLBACK GENERATORS (Rocks, Vehicles, Signals)
        // ═══════════════════════════════════════════════════════════

        // ── Procedural Rock Generator ──
        private static Material _proceduralRockMat;
        private static Mesh _cachedRockMesh;

        private static Material GetProceduralRockMaterial(Shader shader)
        {
            if (_proceduralRockMat == null)
            {
                _proceduralRockMat = new Material(shader);
                _proceduralRockMat.color = new Color(0.48f, 0.47f, 0.45f);
                if (_proceduralRockMat.HasProperty("_Smoothness")) _proceduralRockMat.SetFloat("_Smoothness", 0.05f);
                if (_proceduralRockMat.HasProperty("_Glossiness")) _proceduralRockMat.SetFloat("_Glossiness", 0.05f);
            }
            return _proceduralRockMat;
        }

        private static Mesh CreateProceduralRockMesh()
        {
            if (_cachedRockMesh != null) return _cachedRockMesh;

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

            System.Random rnd = new System.Random(42);
            for (int i = 0; i < baseVerts.Length; i++)
            {
                float disp = 0.85f + (float)rnd.NextDouble() * 0.35f;
                baseVerts[i] *= disp;
                if (baseVerts[i].y < 0) baseVerts[i].y *= 0.65f;
            }

            Vector3[] verts = new Vector3[indices.Length];
            int[] tris = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                verts[i] = baseVerts[indices[i]];
                tris[i] = i;
            }

            Mesh mesh = new Mesh();
            mesh.name = "ProceduralRock";
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _cachedRockMesh = mesh;
            return mesh;
        }

        private GameObject BuildProceduralRock(Vector3 position, float scale, Shader shader)
        {
            return RockBuilder.Build(position, shader, scale);
        }

        // ── Procedural Low-Poly Car Generator ──
        private static readonly Color[] CarColors = new Color[]
        {
            new Color(0.85f, 0.18f, 0.15f), // Crimson red
            new Color(0.18f, 0.42f, 0.78f), // Cobalt blue
            new Color(0.92f, 0.92f, 0.92f), // Arctic white
            new Color(0.22f, 0.22f, 0.24f), // Charcoal dark
            new Color(0.95f, 0.75f, 0.12f), // Taxi yellow
            new Color(0.88f, 0.48f, 0.12f), // Sunset orange
            new Color(0.25f, 0.65f, 0.35f)  // Forest green
        };
        private static List<Material> _sharedCarMaterials;
        private static Material _sharedCarGlassMat;
        private static Material _sharedCarWheelMat;

        private static void EnsureCarMaterials(Shader shader)
        {
            if (_sharedCarMaterials != null && _sharedCarMaterials.Count > 0) return;
            _sharedCarMaterials = new List<Material>();
            foreach (var col in CarColors)
            {
                Material m = new Material(shader);
                m.color = col;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.65f);
                _sharedCarMaterials.Add(m);
            }
            _sharedCarGlassMat = new Material(shader);
            _sharedCarGlassMat.color = new Color(0.15f, 0.20f, 0.25f, 0.9f);
            if (_sharedCarGlassMat.HasProperty("_Smoothness")) _sharedCarGlassMat.SetFloat("_Smoothness", 0.9f);
            if (_sharedCarGlassMat.HasProperty("_Glossiness")) _sharedCarGlassMat.SetFloat("_Glossiness", 0.9f);

            _sharedCarWheelMat = new Material(shader);
            _sharedCarWheelMat.color = new Color(0.12f, 0.12f, 0.12f);
            if (_sharedCarWheelMat.HasProperty("_Smoothness")) _sharedCarWheelMat.SetFloat("_Smoothness", 0.2f);
            if (_sharedCarWheelMat.HasProperty("_Glossiness")) _sharedCarWheelMat.SetFloat("_Glossiness", 0.2f);
        }

        private GameObject CreateProceduralCar(Vector3 position, Quaternion rotation, Shader shader)
        {
            EnsureCarMaterials(shader);
            GameObject car = new GameObject("ProceduralCar");
            car.transform.position = position;
            car.transform.rotation = rotation;

            Material bodyMat = _sharedCarMaterials[Random.Range(0, _sharedCarMaterials.Count)];

            // Body chassis
            GameObject chassis = CreateBoxDirect("Chassis", new Vector3(1.8f, 0.55f, 4.0f), bodyMat);
            chassis.transform.SetParent(car.transform, false);
            chassis.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            // Cabin
            GameObject cabin = CreateBoxDirect("Cabin", new Vector3(1.5f, 0.50f, 2.1f), _sharedCarGlassMat);
            cabin.transform.SetParent(car.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.90f, -0.2f);

            // 4 Wheels
            Vector3[] wheelOffsets = new Vector3[]
            {
                new Vector3(-0.92f, 0.28f, 1.2f),
                new Vector3(0.92f, 0.28f, 1.2f),
                new Vector3(-0.92f, 0.28f, -1.2f),
                new Vector3(0.92f, 0.28f, -1.2f)
            };
            foreach (var wOff in wheelOffsets)
            {
                GameObject wheel = CreateBoxDirect("Wheel", new Vector3(0.24f, 0.48f, 0.56f), _sharedCarWheelMat);
                wheel.transform.SetParent(car.transform, false);
                wheel.transform.localPosition = wOff;
            }

            return car;
        }

        private int PlaceProceduralVehiclesAlongPath(
            List<Vector3> roadPath,
            Shader shader,
            GameObject parent,
            List<Bounds> buildingBounds,
            List<WaterAreaInfo> waterAreas,
            List<WaterwayInfo> waterways,
            float spacing = 32f)
        {
            if (roadPath == null || roadPath.Count < 2) return 0;

            int count = 0;
            float accumulated = 0f;
            bool rightSide = true;

            for (int i = 0; i < roadPath.Count - 1; i++)
            {
                Vector3 a = roadPath[i];
                Vector3 b = roadPath[i + 1];
                float segLen = Vector3.Distance(a, b);
                if (segLen < 10f) continue;
                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                float pos = spacing - accumulated;
                while (pos < segLen)
                {
                    if (Random.value < 0.45f)
                    {
                        Vector3 point = Vector3.Lerp(a, b, pos / segLen);
                        float offset = rightSide ? 2.4f : -2.4f;
                        Vector3 vehiclePos = point + right * offset;
                        vehiclePos.y = 0f;

                        float yAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                        if (!rightSide) yAngle += 180f;

                        if (!IsInsideAnyBuilding(vehiclePos, buildingBounds) &&
                            !WaterBuilder.IsPointInWater(vehiclePos, waterAreas, waterways, 0.5f))
                        {
                            GameObject car = CreateProceduralCar(vehiclePos, Quaternion.Euler(0f, yAngle, 0f), shader);
                            if (car != null)
                            {
                                car.transform.SetParent(parent.transform);
                                count++;
                            }
                        }
                    }

                    rightSide = !rightSide;
                    pos += spacing;
                }

                accumulated = segLen - (pos - spacing);
            }

            return count;
        }

        // ── Procedural Traffic Signal Generator ──
        private static Material _sharedSignalPoleMat;
        private static Material _sharedSignalBoxMat;
        private static Material _sharedSignalRedMat;
        private static Material _sharedSignalYellowMat;
        private static Material _sharedSignalGreenMat;

        private static void EnsureSignalMaterials(Shader shader)
        {
            if (_sharedSignalPoleMat != null) return;
            _sharedSignalPoleMat = new Material(shader);
            _sharedSignalPoleMat.color = new Color(0.22f, 0.22f, 0.24f);

            _sharedSignalBoxMat = new Material(shader);
            _sharedSignalBoxMat.color = new Color(0.15f, 0.15f, 0.16f);

            _sharedSignalRedMat = new Material(shader);
            _sharedSignalRedMat.color = new Color(0.95f, 0.15f, 0.15f);
            if (_sharedSignalRedMat.HasProperty("_EmissionColor"))
            {
                _sharedSignalRedMat.EnableKeyword("_EMISSION");
                _sharedSignalRedMat.SetColor("_EmissionColor", new Color(0.95f, 0.15f, 0.15f) * 0.8f);
            }

            _sharedSignalYellowMat = new Material(shader);
            _sharedSignalYellowMat.color = new Color(0.95f, 0.80f, 0.15f);

            _sharedSignalGreenMat = new Material(shader);
            _sharedSignalGreenMat.color = new Color(0.15f, 0.90f, 0.25f);
            if (_sharedSignalGreenMat.HasProperty("_EmissionColor"))
            {
                _sharedSignalGreenMat.EnableKeyword("_EMISSION");
                _sharedSignalGreenMat.SetColor("_EmissionColor", new Color(0.15f, 0.90f, 0.25f) * 0.8f);
            }
        }

        private GameObject BuildProceduralTrafficSignal(Vector3 position, Quaternion rotation, Shader shader)
        {
            EnsureSignalMaterials(shader);
            GameObject signal = new GameObject("TrafficSignal");
            signal.transform.position = position;
            signal.transform.rotation = rotation;

            // Pole: height 4.2m, 0.12m thick
            GameObject pole = CreateBoxDirect("Pole", new Vector3(0.12f, 4.2f, 0.12f), _sharedSignalPoleMat);
            pole.transform.SetParent(signal.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            // Signal Box: 0.32m wide, 0.85m high, 0.25m deep
            GameObject box = CreateBoxDirect("SignalBox", new Vector3(0.32f, 0.85f, 0.25f), _sharedSignalBoxMat);
            box.transform.SetParent(signal.transform, false);
            box.transform.localPosition = new Vector3(0f, 3.8f, 0.2f);

            // 3 indicator lights
            GameObject redLight = CreateBoxDirect("RedLight", new Vector3(0.18f, 0.18f, 0.08f), _sharedSignalRedMat);
            redLight.transform.SetParent(box.transform, false);
            redLight.transform.localPosition = new Vector3(0f, 0.24f, 0.14f);

            GameObject yellowLight = CreateBoxDirect("YellowLight", new Vector3(0.18f, 0.18f, 0.08f), _sharedSignalYellowMat);
            yellowLight.transform.SetParent(box.transform, false);
            yellowLight.transform.localPosition = new Vector3(0f, 0.0f, 0.14f);

            GameObject greenLight = CreateBoxDirect("GreenLight", new Vector3(0.18f, 0.18f, 0.08f), _sharedSignalGreenMat);
            greenLight.transform.SetParent(box.transform, false);
            greenLight.transform.localPosition = new Vector3(0f, -0.24f, 0.14f);

            return signal;
        }

        private static GameObject CreateBoxDirect(string name, Vector3 size, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;

            Vector3[] verts = new Vector3[24];
            int[] tris = new int[36];

            // +Y Top
            verts[0] = new Vector3(-hx, hy, -hz); verts[1] = new Vector3(hx, hy, -hz);
            verts[2] = new Vector3(hx, hy, hz);   verts[3] = new Vector3(-hx, hy, hz);
            // -Y Bottom
            verts[4] = new Vector3(-hx, -hy, hz);  verts[5] = new Vector3(hx, -hy, hz);
            verts[6] = new Vector3(hx, -hy, -hz); verts[7] = new Vector3(-hx, -hy, -hz);
            // +Z Front
            verts[8] = new Vector3(-hx, -hy, hz);  verts[9] = new Vector3(hx, -hy, hz);
            verts[10] = new Vector3(hx, hy, hz);   verts[11] = new Vector3(-hx, hy, hz);
            // -Z Back
            verts[12] = new Vector3(hx, -hy, -hz); verts[13] = new Vector3(-hx, -hy, -hz);
            verts[14] = new Vector3(-hx, hy, -hz); verts[15] = new Vector3(hx, hy, -hz);
            // -X Left
            verts[16] = new Vector3(-hx, -hy, -hz); verts[17] = new Vector3(-hx, -hy, hz);
            verts[18] = new Vector3(-hx, hy, hz);   verts[19] = new Vector3(-hx, hy, -hz);
            // +X Right
            verts[20] = new Vector3(hx, -hy, hz);  verts[21] = new Vector3(hx, -hy, -hz);
            verts[22] = new Vector3(hx, hy, -hz);  verts[23] = new Vector3(hx, hy, hz);

            for (int f = 0; f < 6; f++)
            {
                int vi = f * 4;
                int ti = f * 6;
                tris[ti] = vi; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
            }

            Mesh m = new Mesh();
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            mf.sharedMesh = m;
            return go;
        }
    }
}


