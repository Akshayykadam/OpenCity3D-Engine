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
        private double _latitude = 50.10152436;
        private double _longitude = 8.66424154;
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

            // Buildings — clean light gray
            Material buildingMat = CreateSolidMaterial(shader, new Color(0.82f, 0.82f, 0.82f), 0.15f);
            // Roofs — slightly darker gray
            Material roofMat = CreateSolidMaterial(shader, new Color(0.72f, 0.72f, 0.72f), 0.1f);

            // Roads — per-type textured materials with normal maps for depth
            Texture2D roadNormalMap = TextureGenerator.CreateAsphaltNormalMap();
            Material motorwayMat = CreateTexturedMaterial(shader, TextureGenerator.CreateMotorwayTexture(), roadNormalMap, 0.05f);
            Material primaryRoadMat = CreateTexturedMaterial(shader, TextureGenerator.CreatePrimaryRoadTexture(), roadNormalMap, 0.05f);
            Material residentialRoadMat = CreateTexturedMaterial(shader, TextureGenerator.CreateResidentialRoadTexture(), roadNormalMap, 0.05f);
            Material footpathMat = CreateTexturedMaterial(shader, TextureGenerator.CreateFootpathTexture(), 0.05f);
            Material crosswalkMat = CreateTexturedMaterial(shader, TextureGenerator.CreateCrosswalkTexture(), 0.05f);

            Dictionary<string, Material> roadMaterials = new Dictionary<string, Material>
            {
                { "motorway", motorwayMat },
                { "primary", primaryRoadMat },
                { "residential", residentialRoadMat },
                { "footpath", footpathMat }
            };

            // Sidewalks — paver texture
            Material sidewalkMat = CreateTexturedMaterial(shader, TextureGenerator.CreateSidewalkTexture(), 0.1f);
            // Parks — lush green texture
            Material parkMat = CreateTexturedMaterial(shader, TextureGenerator.CreateParkTexture(), 0.05f);
            // Water — deep blue texture
            Material waterMat = CreateTexturedMaterial(shader, TextureGenerator.CreateWaterTexture(), 0.6f);
            // Beach/Sand — warm tan
            Material beachMat = CreateSolidMaterial(shader, new Color(0.82f, 0.72f, 0.52f), 0.05f);
            // Ground/Base — earth texture
            Material groundMat = CreateTexturedMaterial(shader, TextureGenerator.CreateGroundTexture(), 0.1f);
            // Platform sides — darker solid
            Material platformMat = CreateSolidMaterial(shader, new Color(0.28f, 0.28f, 0.30f), 0.15f);

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

            int buildingCount = 0, roadCount = 0, parkCount = 0, waterCount = 0, beachCount = 0, treeCount = 0;
            int intersectionCount = 0;

            // Clear intersection data from previous generation
            RoadBuilder.ClearIntersectionData();

            List<Bounds> buildingBounds = new List<Bounds>();
            List<Vector3> parkCenters = new List<Vector3>();
            List<float> parkSizes = new List<float>();

            foreach (var way in data.Ways)
            {
                if (way.HasTag("building"))
                {
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
                    GameObject road = RoadBuilder.Build(way, data, roadMaterials, sidewalkMat, shifter);
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

            // 7. Generate intersection fills where roads meet
            intersectionCount = GenerateIntersections(intersectionsParent.transform,
                residentialRoadMat);

            // 8. Dense trees in parks (like the reference images)
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

            // 10. Street lights along roads
            int streetLightCount = 0;
            GameObject lightsParent = new GameObject("StreetLights");
            lightsParent.transform.SetParent(cityRoot.transform);

            foreach (var way in data.Ways)
            {
                if (!way.HasTag("highway")) continue;
                string hwType = (way.GetTag("highway") ?? "").ToLower();
                // Skip footways and paths — no street lights
                if (hwType == "footway" || hwType == "path" || hwType == "steps" || hwType == "cycleway") continue;

                List<Vector3> roadPath = new List<Vector3>();
                foreach (long nodeId in way.NodeIds)
                {
                    if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                        roadPath.Add(shifter.GetLocalPosition(node.Latitude, node.Longitude));
                }

                List<GameObject> lights = StreetFurnitureBuilder.PlaceStreetLights(roadPath, shader, 25f);
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
            }

            // 11. Scene atmosphere (lighting, fog, post-processing)
            SceneSetup.Setup(_radius);

            Debug.Log($"Generation Complete! Buildings: {buildingCount}, Roads: {roadCount}, Intersections: {intersectionCount}, Parks: {parkCount}, Water: {waterCount}, Beaches: {beachCount}, Trees: {treeCount}, StreetLights: {streetLightCount}");
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
        /// Clusters nearby road endpoints and generates flat disc fills
        /// to cover the gaps where roads meet at junctions.
        /// </summary>
        private int GenerateIntersections(Transform parent, Material material)
        {
            var endpoints = RoadBuilder.GetRoadEndpoints();
            var widths = RoadBuilder.GetRoadWidths();

            if (endpoints.Count < 2) return 0;

            float clusterRadius = 8f; // Merge endpoints within 8m
            bool[] used = new bool[endpoints.Count];
            int intersectionCount = 0;

            for (int i = 0; i < endpoints.Count; i++)
            {
                if (used[i]) continue;

                // Find cluster of nearby endpoints
                List<int> cluster = new List<int> { i };
                used[i] = true;

                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    if (used[j]) continue;
                    if (Vector3.Distance(endpoints[i], endpoints[j]) < clusterRadius)
                    {
                        cluster.Add(j);
                        used[j] = true;
                    }
                }

                // Only generate intersection if 3+ road ends meet (T or cross junction)
                if (cluster.Count < 3) continue;

                // Compute center and max width
                Vector3 center = Vector3.zero;
                float maxWidth = 0f;
                foreach (int idx in cluster)
                {
                    center += endpoints[idx];
                    if (idx < widths.Count)
                        maxWidth = Mathf.Max(maxWidth, widths[idx]);
                }
                center /= cluster.Count;
                center.y = 0.09f; // Just above road surface

                float discRadius = Mathf.Max(maxWidth * 0.7f, 4f);

                GameObject disc = CreateIntersectionDisc(center, discRadius, material,
                    $"Intersection_{intersectionCount}");
                disc.transform.SetParent(parent);
                intersectionCount++;
            }

            return intersectionCount;
        }

        private GameObject CreateIntersectionDisc(Vector3 center, float radius, Material material, string name)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            int segments = 16;
            Mesh mesh = new Mesh();
            Vector3[] verts = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            int[] tris = new int[segments * 3];

            verts[0] = center;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts[i + 1] = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                uvs[i + 1] = new Vector2(
                    0.5f + Mathf.Cos(angle) * 0.5f,
                    0.5f + Mathf.Sin(angle) * 0.5f);

                int next = (i + 1) % segments + 1;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = next;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            return go;
        }
    }
}

