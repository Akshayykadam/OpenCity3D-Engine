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
        private double _latitude = 40.65370764935582;
        private double _longitude = -73.98882885896016;
        private float _radius = 1000f;
        
        private CityController _cityController;
        private bool _isGenerating = false;

        [MenuItem("GeoCity3D/City Generator")]
        public static void ShowWindow()
        {
            GetWindow<CityGeneratorWindow>("City Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("GeoCity3D Generator", EditorStyles.boldLabel);

            _latitude = EditorGUILayout.DoubleField("Latitude", _latitude);
            _longitude = EditorGUILayout.DoubleField("Longitude", _longitude);
            _radius = EditorGUILayout.FloatField("Radius (m)", _radius);

            _cityController = (CityController)EditorGUILayout.ObjectField("City Controller", _cityController, typeof(CityController), true);

            if (GUILayout.Button("Generate City"))
            {
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

        // ── Solid color material — renders BOTH sides so geometry never looks hollow ──
        private Material CreateSolidMaterial(Shader shader, Color color, float smoothness = 0.3f)
        {
            Material mat = new Material(shader);
            mat.color = color;
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
            mat.color = Color.white;
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
            mat.color = new Color(0.12f, 0.35f, 0.55f, 0.85f); // Deep translucent blue
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.95f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.4f);
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
            var shifter = FindObjectOfType<OriginShifter>();
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

            // ═══════════════════════════════════════════════════════════
            // 5. MATERIALS — Procedural textured materials
            // ═══════════════════════════════════════════════════════════

            // ── Building Materials ──
            // Procedural mode: pool of solid-color materials (no textures, clean look)
            // Prefab mode: materials come from the FBX models themselves
            List<Material> wallPool = new List<Material>();
            List<Material> roofPool = new List<Material>();

            if (_cityController.BuildingGenerationMode == BuildingMode.Procedural)
            {
                // Create 10 wall colors + 4 roof colors with PBR tuning
                for (int i = 0; i < 10; i++)
                {
                    Color wallColor = TextureGenerator.GetRandomWallColor();
                    Material wm = CreateSolidMaterial(shader, wallColor, 0.35f);
                    // Slight metallic for realistic plaster/concrete
                    if (wm.HasProperty("_Metallic")) wm.SetFloat("_Metallic", 0.05f);
                    wallPool.Add(wm);
                }
                for (int i = 0; i < 4; i++)
                {
                    Color roofColor = TextureGenerator.GetRandomRoofColor();
                    Material rm = CreateSolidMaterial(shader, roofColor, 0.15f);
                    if (rm.HasProperty("_Metallic")) rm.SetFloat("_Metallic", 0.02f);
                    roofPool.Add(rm);
                }
            }

            // Always create at least one fallback material for buildings/roofs
            Material buildingMat = CreateSolidMaterial(shader, new Color(0.82f, 0.82f, 0.82f), 0.15f);
            Material roofMat = CreateSolidMaterial(shader, new Color(0.55f, 0.53f, 0.50f), 0.1f);
            
            if (wallPool.Count > 0) buildingMat = wallPool[0];
            if (roofPool.Count > 0) roofMat = roofPool[0];

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
            GameObject beachesParent = new GameObject("Beaches");
            beachesParent.transform.SetParent(cityRoot.transform);
            GameObject vehiclesParent = new GameObject("Vehicles");
            vehiclesParent.transform.SetParent(cityRoot.transform);
            GameObject propsParent = new GameObject("StreetProps");
            propsParent.transform.SetParent(cityRoot.transform);
            GameObject signalsParent = new GameObject("TrafficSignals");
            signalsParent.transform.SetParent(cityRoot.transform);
            GameObject lotFillParent = new GameObject("LotFill");
            lotFillParent.transform.SetParent(cityRoot.transform);

            int buildingCount = 0, roadCount = 0, parkCount = 0, waterCount = 0, beachCount = 0, treeCount = 0;
            int intersectionCount = 0, vehicleCount = 0, propCount = 0, signalCount = 0, lotFillCount = 0;

            // Clear intersection data from previous generation
            RoadBuilder.ClearIntersectionData();

            List<Bounds> buildingBounds = new List<Bounds>();
            List<Bounds> roadBounds = new List<Bounds>();
            List<Vector3> parkCenters = new List<Vector3>();
            List<float> parkSizes = new List<float>();

            foreach (var way in data.Ways)
            {
                if (way.HasTag("building"))
                {
                    GameObject building = null;

                    if (_cityController.BuildingGenerationMode == BuildingMode.Prefab
                        && _cityController.BuildingPrefabs != null
                        && _cityController.BuildingPrefabs.Length > 0)
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

                            GameObject prefab = _cityController.BuildingPrefabs[Random.Range(0, _cityController.BuildingPrefabs.Length)];
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
                    else
                    {
                        // ── PROCEDURAL MODE ── solid-color buildings
                        Material thisWall = wallPool.Count > 0 ? wallPool[Random.Range(0, wallPool.Count)] : buildingMat;
                        Material thisRoof = roofPool.Count > 0 ? roofPool[Random.Range(0, roofPool.Count)] : roofMat;
                        building = BuildingBuilder.Build(way, data, thisWall, thisRoof, shifter);
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
                    // Skip noisy minor pedestrian paths to keep the 3D road network clean
                    if (RoadBuilder.FootpathTypes.Contains(hwType)) continue;

                    GameObject road = RoadBuilder.Build(way, data, roadMaterials, sidewalkMat, shifter);
                    if (road != null)
                    {
                        road.transform.SetParent(roadsParent.transform);
                        roadCount++;

                        // Track road bounds for lot filler
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
                    }
                }
                else if (IsWaterArea(way))
                {
                    GameObject water = AreaBuilder.Build(way, data, waterMat, shifter, -0.15f, "Water");
                    if (water != null)
                    {
                        water.transform.SetParent(waterParent.transform);
                        waterCount++;
                    }
                }
                else if (IsLinearWaterway(way))
                {
                    float riverWidth = DetermineRiverWidth(way);
                    // Use RoadBuilder's backward-compatible overload for water strips
                    GameObject river = RoadBuilder.Build(way, data, waterMat, shifter, riverWidth);
                    if (river != null)
                    {
                        river.name = $"River_{way.Id}";
                        river.transform.SetParent(waterParent.transform);
                        waterCount++;
                    }
                }
            }

            // 7. Generate intersection fills where roads meet
            intersectionCount = GenerateIntersections(intersectionsParent.transform,
                intersectionMat);

            // Collect intersection centers for traffic signal placement
            List<Vector3> intersectionCenters = new List<Vector3>();
            var roadEndsForSignals = RoadBuilder.GetRoadEnds();
            {
                float clusterR = 15f;
                bool[] usedSignal = new bool[roadEndsForSignals.Count];
                for (int i = 0; i < roadEndsForSignals.Count; i++)
                {
                    if (usedSignal[i]) continue;
                    List<int> cluster = new List<int> { i };
                    usedSignal[i] = true;
                    for (int j = i + 1; j < roadEndsForSignals.Count; j++)
                    {
                        if (usedSignal[j]) continue;
                        if (Vector3.Distance(roadEndsForSignals[i].Position, roadEndsForSignals[j].Position) < clusterR)
                        {
                            cluster.Add(j);
                            usedSignal[j] = true;
                        }
                    }
                    if (cluster.Count >= 2)
                    {
                        Vector3 center = Vector3.zero;
                        foreach (int idx in cluster) center += roadEndsForSignals[idx].Position;
                        center /= cluster.Count;
                        center.y = 0f;
                        intersectionCenters.Add(center);
                    }
                }
            }

            // ── Determine if we have SimplePoly prefabs ──
            bool hasTreePrefabs = _cityController.TreePrefabs != null && _cityController.TreePrefabs.Length > 0;
            bool hasBushPrefabs = _cityController.BushPrefabs != null && _cityController.BushPrefabs.Length > 0;
            bool hasRockPrefabs = _cityController.RockPrefabs != null && _cityController.RockPrefabs.Length > 0;
            bool hasLightPrefabs = _cityController.StreetLightPrefabs != null && _cityController.StreetLightPrefabs.Length > 0;
            bool hasSignalPrefabs = _cityController.TrafficSignalPrefabs != null && _cityController.TrafficSignalPrefabs.Length > 0;
            bool hasPropPrefabs = _cityController.StreetPropPrefabs != null && _cityController.StreetPropPrefabs.Length > 0;
            bool hasVehiclePrefabs = _cityController.VehiclePrefabs != null && _cityController.VehiclePrefabs.Length > 0;

            // 8. Dense trees in parks
            for (int i = 0; i < parkCenters.Count; i++)
            {
                float parkRadius = Mathf.Max(parkSizes[i] * 0.85f, 8f);
                int treeCountInPark = Mathf.Clamp(Mathf.RoundToInt(parkRadius * parkRadius * 0.04f), 8, 80);

                if (hasTreePrefabs)
                {
                    // Use prefab-based park nature with trees, bushes, and rocks
                    List<GameObject> parkNature = TreeBuilder.ScatterParkNature(
                        parkCenters[i], parkRadius, treeCountInPark,
                        _cityController.TreePrefabs, _cityController.BushPrefabs, _cityController.RockPrefabs);
                    foreach (var obj in parkNature)
                    {
                        obj.transform.SetParent(treesParent.transform);
                        treeCount++;
                    }
                }
                else
                {
                    // Fallback: procedural trees
                    List<GameObject> parkTrees = TreeBuilder.ScatterTrees(parkCenters[i], parkRadius, treeCountInPark, shader);
                    foreach (var t in parkTrees)
                    {
                        t.transform.SetParent(treesParent.transform);
                        treeCount++;
                    }
                }
            }

            // 9. Street trees along roads
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
                    if (segLen < 12f) continue;

                    Vector3 dir = (roadPath[i + 1] - roadPath[i]).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                    int treesAlongSeg = Mathf.FloorToInt(segLen / 18f);
                    for (int t = 0; t < treesAlongSeg; t++)
                    {
                        if (Random.value > 0.5f) continue;

                        float tPos = (t + 0.5f) / Mathf.Max(treesAlongSeg, 1);
                        Vector3 pos = Vector3.Lerp(roadPath[i], roadPath[i + 1], tPos);

                        float side = Random.value > 0.5f ? 1f : -1f;
                        Vector3 treePos = pos + right * side * (5f + Random.Range(0f, 2f));

                        if (!IsInsideAnyBuilding(treePos, buildingBounds))
                        {
                            GameObject tree;
                            if (hasTreePrefabs)
                                tree = TreeBuilder.BuildPrefab(treePos, _cityController.TreePrefabs, Random.Range(0.5f, 1.0f));
                            else
                                tree = TreeBuilder.Build(treePos, shader, Random.Range(0.5f, 1.0f));

                            if (tree != null)
                            {
                                tree.transform.SetParent(treesParent.transform);
                                treeCount++;
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

                List<GameObject> lights;
                if (hasLightPrefabs)
                    lights = StreetFurnitureBuilder.PlaceStreetLightPrefabs(roadPath, _cityController.StreetLightPrefabs, 25f);
                else
                    lights = StreetFurnitureBuilder.PlaceStreetLights(roadPath, shader, 25f);

                foreach (var light in lights)
                {
                    if (!IsInsideAnyBuilding(light.transform.position, buildingBounds))
                    {
                        light.transform.SetParent(lightsParent.transform);
                        streetLightCount++;
                    }
                    else
                    {
                        Object.DestroyImmediate(light);
                    }
                }

                // 11b. Street props along this road
                if (hasPropPrefabs)
                {
                    List<GameObject> props = StreetFurnitureBuilder.PlaceStreetProps(
                        roadPath, _cityController.StreetPropPrefabs, 40f);
                    foreach (var prop in props)
                    {
                        if (!IsInsideAnyBuilding(prop.transform.position, buildingBounds))
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
                if (hasVehiclePrefabs)
                {
                    List<GameObject> cars = VehicleBuilder.PlaceParkedVehicles(
                        roadPath, _cityController.VehiclePrefabs, 30f);
                    foreach (var car in cars)
                    {
                        if (!IsInsideAnyBuilding(car.transform.position, buildingBounds))
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
            if (hasSignalPrefabs && intersectionCenters.Count > 0)
            {
                List<GameObject> signals = StreetFurnitureBuilder.PlaceTrafficSignals(
                    intersectionCenters, _cityController.TrafficSignalPrefabs);
                foreach (var signal in signals)
                {
                    signal.transform.SetParent(signalsParent.transform);
                    signalCount++;
                }
            }

            // 13. Fill empty lots with vegetation
            Bounds cityBounds = new Bounds(Vector3.zero, Vector3.one * _radius * 2f);
            lotFillCount = LotFiller.FillEmptyLots(
                cityBounds, buildingBounds, roadBounds,
                _cityController.TreePrefabs,
                _cityController.BushPrefabs,
                _cityController.RockPrefabs,
                lotFillParent.transform);
            Debug.Log($"Lot Fill: placed {lotFillCount} vegetation objects in empty spaces.");

            // 14. Scene atmosphere (lighting, fog, post-processing)
            SceneSetup.Setup(_radius);

            // 15. Optimization: Combine meshes by material (skip buildings — LODGroup needs individual renderers)
            // Buildings have LODGroup so we don't combine them
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(roadsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(intersectionsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(parksParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(waterParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(beachesParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(treesParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(lightsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(vehiclesParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(propsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(signalsParent);
            GeoCity3D.Visuals.CityCombiner.CombineMeshesByMaterial(lotFillParent);

            // 16. Enhanced Occlusion Culling + Static Batching
            // Buildings: full occlusion (occluder + occludee)
            SetStaticFlags(buildingsParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            // Smaller objects: occludee only (hidden by buildings, but don't block others)
            SetStaticFlags(treesParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(lightsParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(vehiclesParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(propsParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(signalsParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(lotFillParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            // Infrastructure: static batching + occluder
            SetStaticFlags(roadsParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(intersectionsParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(parksParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(waterParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            SetStaticFlags(beachesParent, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            Debug.Log($"Generation Complete! Buildings: {buildingCount}, Roads: {roadCount}, Intersections: {intersectionCount}, Parks: {parkCount}, Water: {waterCount}, Beaches: {beachCount}, Trees: {treeCount}, StreetLights: {streetLightCount}, Vehicles: {vehicleCount}, Props: {propCount}, TrafficSignals: {signalCount}, LotFill: {lotFillCount}");
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
            GameObjectUtility.SetStaticEditorFlags(go, flags);
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
            List<GameObject> small = new List<GameObject>();
            List<GameObject> tall = new List<GameObject>();

            foreach (var prefab in allPrefabs)
            {
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
                pool = new List<GameObject>(allPrefabs); // fallback

            return pool[Random.Range(0, pool.Count)];
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
        //  INTERSECTION FILL GENERATION
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Generates custom polygon meshes that seamlessly connect the incoming roads.
        /// </summary>
        private int GenerateIntersections(Transform parent, Material defaultMat)
        {
            var roadEnds = RoadBuilder.GetRoadEnds();
            if (roadEnds.Count < 2) return 0;

            float clusterRadius = 15f; // Merge endpoints within 15m
            bool[] used = new bool[roadEnds.Count];
            int intersectionCount = 0;

            Material sidewalkMat = _cityController.SidewalkMaterial != null
                ? _cityController.SidewalkMaterial : defaultMat;

            for (int i = 0; i < roadEnds.Count; i++)
            {
                if (used[i]) continue;

                // Find cluster of nearby endpoints
                List<int> cluster = new List<int> { i };
                used[i] = true;

                for (int j = i + 1; j < roadEnds.Count; j++)
                {
                    if (used[j]) continue;
                    if (Vector3.Distance(roadEnds[i].Position, roadEnds[j].Position) < clusterRadius)
                    {
                        cluster.Add(j);
                        used[j] = true;
                    }
                }

                if (cluster.Count < 2) continue;

                // Compute center
                Vector3 center = Vector3.zero;
                foreach (int idx in cluster) center += roadEnds[idx].Position;
                center /= cluster.Count;
                center.y = 0f;

                List<Vector3> roadPoly = new List<Vector3>();
                List<Vector3> sidewalkPoly = new List<Vector3>();

                // Build a radial map of corners
                foreach (int idx in cluster)
                {
                    var end = roadEnds[idx];
                    Vector3 fwd = end.Direction; // Direction pointing OUT of the road (towards center)
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                    float halfRoad = end.Width / 2f;
                    float sidewalkWidth = 1.5f;

                    // Road corners
                    Vector3 cornerLeft = end.Position - right * halfRoad;
                    Vector3 cornerRight = end.Position + right * halfRoad;
                    cornerLeft.y = 0.10f; // Road level
                    cornerRight.y = 0.10f;
                    roadPoly.Add(cornerLeft);
                    roadPoly.Add(cornerRight);

                    // Sidewalk corners
                    Vector3 swLeft = end.Position - right * (halfRoad + sidewalkWidth);
                    Vector3 swRight = end.Position + right * (halfRoad + sidewalkWidth);
                    // Push them slightly further out to ensure coverage
                    swLeft += fwd * 0.5f;
                    swRight += fwd * 0.5f;
                    swLeft.y = 0.19f; // Sidewalk level
                    swRight.y = 0.19f;
                    sidewalkPoly.Add(swLeft);
                    sidewalkPoly.Add(swRight);
                }

                // Build a convex hull from the collected corner points
                roadPoly = GeoCity3D.Geometry.GeometryUtils.GetConvexHull(roadPoly);
                sidewalkPoly = GeoCity3D.Geometry.GeometryUtils.GetConvexHull(sidewalkPoly);

                // Use the material of the largest road in the cluster
                Material roadMat = roadEnds[cluster[0]].Material;
                float maxWidth = roadEnds[cluster[0]].Width;
                foreach (int idx in cluster)
                {
                    if (roadEnds[idx].Width > maxWidth)
                    {
                        maxWidth = roadEnds[idx].Width;
                        roadMat = roadEnds[idx].Material;
                    }
                }

                // Build exact-fit meshes
                GameObject roadMeshObj = CreatePolygonMesh(roadPoly, roadMat, $"Intersection_{intersectionCount}");
                if (roadMeshObj != null) roadMeshObj.transform.SetParent(parent);

                GameObject swMeshObj = CreatePolygonMesh(sidewalkPoly, sidewalkMat, $"IntersectionSW_{intersectionCount}");
                if (swMeshObj != null) swMeshObj.transform.SetParent(parent);

                intersectionCount++;
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
    }
}


