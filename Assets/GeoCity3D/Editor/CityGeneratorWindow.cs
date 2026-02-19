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

namespace GeoCity3D.Editor
{
    public class CityGeneratorWindow : EditorWindow
    {
        private double _latitude = 51.5025605;
        private double _longitude = -0.0811455;
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
            // Smoothness for slight sheen
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            // Render both sides — never see through geometry
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // 0 = Off (double-sided)
            // Ensure opaque rendering
            mat.renderQueue = 2000; // Geometry queue
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
            // 5. MATERIALS — Clean architectural model palette
            //    Light gray buildings, dark roads, green parks, dark base
            // ═══════════════════════════════════════════════════════════

            // Buildings — clean light gray with slight warmth
            Material buildingMat = CreateSolidMaterial(shader, new Color(0.82f, 0.82f, 0.82f), 0.15f);
            // Roofs — slightly darker gray
            Material roofMat = CreateSolidMaterial(shader, new Color(0.72f, 0.72f, 0.72f), 0.1f);
            // Roads — dark charcoal
            Material roadMat = CreateSolidMaterial(shader, new Color(0.22f, 0.22f, 0.24f), 0.05f);
            // Sidewalks — medium gray
            Material sidewalkMat = CreateSolidMaterial(shader, new Color(0.60f, 0.60f, 0.60f), 0.1f);
            // Parks — vibrant green
            Material parkMat = CreateSolidMaterial(shader, new Color(0.18f, 0.55f, 0.12f), 0.05f);
            // Water — dark teal
            Material waterMat = CreateSolidMaterial(shader, new Color(0.15f, 0.30f, 0.38f), 0.6f);
            // Ground/Base — dark gray
            Material groundMat = CreateSolidMaterial(shader, new Color(0.35f, 0.35f, 0.37f), 0.1f);
            // Platform sides — darker
            Material platformMat = CreateSolidMaterial(shader, new Color(0.28f, 0.28f, 0.30f), 0.15f);

            // 6. Generate Geometry
            GameObject cityRoot = new GameObject("GeneratedCity");
            cityRoot.transform.position = Vector3.zero;

            GameObject buildingsParent = new GameObject("Buildings");
            buildingsParent.transform.SetParent(cityRoot.transform);
            GameObject roadsParent = new GameObject("Roads");
            roadsParent.transform.SetParent(cityRoot.transform);
            GameObject parksParent = new GameObject("Parks");
            parksParent.transform.SetParent(cityRoot.transform);
            GameObject waterParent = new GameObject("Water");
            waterParent.transform.SetParent(cityRoot.transform);
            GameObject treesParent = new GameObject("Trees");
            treesParent.transform.SetParent(cityRoot.transform);

            int buildingCount = 0, roadCount = 0, parkCount = 0, waterCount = 0, treeCount = 0;

            List<Bounds> buildingBounds = new List<Bounds>();
            List<Vector3> parkCenters = new List<Vector3>();
            List<float> parkSizes = new List<float>();

            foreach (var way in data.Ways)
            {
                if (way.HasTag("building"))
                {
                    // No atlas needed — single solid color for all buildings
                    GameObject building = BuildingBuilder.Build(way, data,
                        buildingMat, roofMat, shifter);

                    if (building != null)
                    {
                        building.transform.SetParent(buildingsParent.transform);
                        buildingCount++;

                        var renderer = building.GetComponent<MeshRenderer>();
                        if (renderer != null)
                            buildingBounds.Add(renderer.bounds);
                    }
                }
                else if (way.HasTag("highway"))
                {
                    GameObject road = RoadBuilder.Build(way, data, roadMat, sidewalkMat, shifter);
                    if (road != null)
                    {
                        road.transform.SetParent(roadsParent.transform);
                        roadCount++;
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
                else if (IsWaterArea(way))
                {
                    GameObject water = AreaBuilder.Build(way, data, waterMat, shifter, 0.01f, "Water");
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

            // 7. Dense trees in parks (like the reference images)
            for (int i = 0; i < parkCenters.Count; i++)
            {
                float parkRadius = Mathf.Max(parkSizes[i] * 0.85f, 8f);
                int treeCountInPark = Mathf.Clamp(Mathf.RoundToInt(parkRadius * parkRadius * 0.04f), 8, 80);
                List<GameObject> parkTrees = TreeBuilder.ScatterTrees(parkCenters[i], parkRadius, treeCountInPark, shader);
                foreach (var t in parkTrees)
                {
                    t.transform.SetParent(treesParent.transform);
                    treeCount++;
                }
            }

            // 8. Street trees along roads
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
                            GameObject tree = TreeBuilder.Build(treePos, shader, Random.Range(0.5f, 1.0f));
                            tree.transform.SetParent(treesParent.transform);
                            treeCount++;
                        }
                    }
                }
            }

            // 9. Generate raised platform base
            GameObject ground = GroundBuilder.Build(_radius, groundMat, platformMat);
            ground.transform.SetParent(cityRoot.transform);

            Debug.Log($"Generation Complete! Buildings: {buildingCount}, Roads: {roadCount}, Parks: {parkCount}, Water: {waterCount}, Trees: {treeCount}");
            _isGenerating = false;
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

        private bool IsWaterArea(OsmWay way)
        {
            string natural = (way.GetTag("natural") ?? "").ToLower();
            string waterway = (way.GetTag("waterway") ?? "").ToLower();
            string water = (way.GetTag("water") ?? "").ToLower();
            string landuse = (way.GetTag("landuse") ?? "").ToLower();
            return natural == "water" || natural == "bay" || natural == "wetland"
                || waterway == "riverbank" || waterway == "dock" || waterway == "boatyard"
                || water.Length > 0 || landuse == "reservoir" || landuse == "basin";
        }

        private bool IsLinearWaterway(OsmWay way)
        {
            string waterway = (way.GetTag("waterway") ?? "").ToLower();
            return waterway == "river" || waterway == "stream" || waterway == "canal"
                || waterway == "drain" || waterway == "ditch";
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
    }
}
