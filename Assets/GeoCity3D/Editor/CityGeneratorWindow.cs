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
        private double _latitude = 18.4401326;
        private double _longitude = 73.7747237;
        private float _radius = 500f;
        
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

        private Shader FindBestShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            return shader;
        }

        private Material CreateMaterial(Shader shader, Texture2D texture)
        {
            Material mat = new Material(shader);
            mat.mainTexture = texture;
            return mat;
        }

        private Material CreateVertexColorMaterial(Shader shader)
        {
            Material mat = new Material(shader);
            mat.color = Color.white;
            mat.EnableKeyword("_VERTEX_COLORS");
            return mat;
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

            // 5. Build Texture Atlas
            Debug.Log("Building texture atlas...");
            TextureAtlas atlas = new TextureAtlas();
            atlas.Build();

            Material wallAtlasMat = CreateMaterial(shader, atlas.WallAtlas);
            Material roofAtlasMat = CreateMaterial(shader, atlas.RoofAtlas);
            Debug.Log("Atlas built (16 facade + 16 roof variations).");

            // 6. Create materials (all procedural — realistic for Indian context)
            Material roadMat = CreateMaterial(shader, TextureGenerator.CreateRoadTexture());
            Material sidewalkMat = CreateMaterial(shader, TextureGenerator.CreateSidewalkTexture());
            Material parkMat = CreateMaterial(shader, TextureGenerator.CreateParkTexture());
            Material waterMat = CreateMaterial(shader, TextureGenerator.CreateWaterTexture());
            Material groundMat = CreateMaterial(shader, TextureGenerator.CreateGroundTexture());
            Material treeMat = CreateVertexColorMaterial(shader);

            // 7. Generate Geometry
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

            // Track building positions to avoid placing trees inside buildings
            List<Bounds> buildingBounds = new List<Bounds>();

            // Track park centers for tree placement
            List<Vector3> parkCenters = new List<Vector3>();
            List<float> parkSizes = new List<float>();

            foreach (var way in data.Ways)
            {
                if (way.HasTag("building"))
                {
                    Vector2 wOff, wScl, rOff, rScl;
                    atlas.GetRandomWallTile(out wOff, out wScl);
                    atlas.GetRandomRoofTile(out rOff, out rScl);

                    GameObject building = BuildingBuilder.Build(way, data,
                        wallAtlasMat, roofAtlasMat,
                        wOff, wScl, rOff, rScl, shifter);

                    if (building != null)
                    {
                        building.transform.SetParent(buildingsParent.transform);
                        buildingCount++;

                        // Track bounds for tree placement avoidance
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
                    GameObject park = AreaBuilder.Build(way, data, parkMat, shifter, 0.03f, "Park");
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
            }

            // 8. Dense trees in parks
            for (int i = 0; i < parkCenters.Count; i++)
            {
                float parkRadius = Mathf.Max(parkSizes[i] * 0.85f, 8f);
                int treeCountInPark = Mathf.Clamp(Mathf.RoundToInt(parkRadius * parkRadius * 0.03f), 5, 60);
                List<GameObject> parkTrees = TreeBuilder.ScatterTrees(parkCenters[i], parkRadius, treeCountInPark, treeMat);
                foreach (var t in parkTrees)
                {
                    t.transform.SetParent(treesParent.transform);
                    treeCount++;
                }
            }

            // 9. Street trees along all roads (much denser)
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
                    if (segLen < 8f) continue;

                    Vector3 dir = (roadPath[i + 1] - roadPath[i]).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                    // Place trees every ~15m along segment
                    int treesAlongSeg = Mathf.FloorToInt(segLen / 15f);
                    for (int t = 0; t < treesAlongSeg; t++)
                    {
                        if (Random.value > 0.6f) continue; // Skip some for variety

                        float tPos = (t + 0.5f) / Mathf.Max(treesAlongSeg, 1);
                        Vector3 pos = Vector3.Lerp(roadPath[i], roadPath[i + 1], tPos);

                        // Place on both sides
                        for (int side = -1; side <= 1; side += 2)
                        {
                            if (Random.value > 0.5f) continue; // Not always both sides

                            Vector3 treePos = pos + right * side * (5.5f + Random.Range(0f, 3f));

                            // Check it's not inside a building
                            if (!IsInsideAnyBuilding(treePos, buildingBounds))
                            {
                                GameObject tree = TreeBuilder.Build(treePos, treeMat, Random.Range(0.6f, 1.2f));
                                tree.transform.SetParent(treesParent.transform);
                                treeCount++;
                            }
                        }
                    }
                }
            }

            // 10. Scatter additional trees in open spaces between buildings
            int urbanTreeCount = Mathf.Clamp(buildingCount / 2, 30, 400);
            for (int i = 0; i < urbanTreeCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(Random.value) * _radius * 0.9f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);

                if (!IsInsideAnyBuilding(pos, buildingBounds))
                {
                    GameObject tree = TreeBuilder.Build(pos, treeMat, Random.Range(0.5f, 1.3f));
                    tree.transform.SetParent(treesParent.transform);
                    treeCount++;
                }
            }

            // 11. Generate Ground Plane
            GameObject ground = GroundBuilder.Build(_radius, groundMat);
            ground.transform.SetParent(cityRoot.transform);

            Debug.Log($"Generation Complete! Buildings: {buildingCount}, Roads: {roadCount}, Parks: {parkCount}, Water: {waterCount}, Trees: {treeCount}");
            _isGenerating = false;
        }

        private bool IsInsideAnyBuilding(Vector3 pos, List<Bounds> buildingBounds)
        {
            // Quick check — is the position XZ inside any building's bounding box?
            Vector3 testPos = new Vector3(pos.x, 5f, pos.z); // Test at building height
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
            return natural == "water" || waterway == "riverbank" || waterway == "dock"
                || water.Length > 0 || landuse == "reservoir" || landuse == "basin";
        }

        private bool IsTreeNode(OsmWay way)
        {
            string natural = (way.GetTag("natural") ?? "").ToLower();
            return natural == "tree_row" || natural == "tree";
        }
    }
}
