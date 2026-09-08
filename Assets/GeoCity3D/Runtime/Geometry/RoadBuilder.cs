using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using System.Linq;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates solid road meshes with thickness.
    /// Detects OSM bridge tags and creates elevated bridge decks with support pillars.
    /// </summary>
    public class RoadBuilder
    {
        private const float ROAD_Y_SURFACE = 0.08f;
        private const float ROAD_THICKNESS = 0.12f;
        private const float SIDEWALK_Y_SURFACE = 0.18f;
        private const float SIDEWALK_THICKNESS = 0.18f;
        private const float SIDEWALK_WIDTH = 2.0f;

        // Bridge constants
        private const float BRIDGE_ELEVATION = 5.0f;    // Height above ground
        private const float BRIDGE_DECK_THICKNESS = 0.6f; // Thicker deck for bridges
        private const float BRIDGE_RAIL_HEIGHT = 1.0f;    // Side railing height
        private const float BRIDGE_RAIL_THICKNESS = 0.15f;
        private const float PILLAR_WIDTH = 0.8f;
        private const float PILLAR_SPACING = 20f;         // One pillar every 20m

        // ── Road type categories for material selection ──
        public static readonly string[] MotorwayTypes = { "motorway", "motorway_link", "trunk", "trunk_link" };
        public static readonly string[] PrimaryTypes = { "primary", "primary_link", "secondary", "secondary_link" };
        public static readonly string[] ResidentialTypes = { "tertiary", "tertiary_link", "residential", "unclassified", "living_street", "service" };
        public static readonly string[] FootpathTypes = { "footway", "path", "pedestrian", "cycleway", "steps", "track" };

        // ── Intersection endpoint registry ──
        public struct RoadEnd
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Width;
            public Material Material;
            public string RoadClass;
        }

        private static List<RoadEnd> _roadEnds = new List<RoadEnd>();

        public static void ClearIntersectionData()
        {
            _roadEnds.Clear();
            RoadSpatialIndex.Clear();
        }

        public static List<RoadEnd> GetRoadEnds() => _roadEnds;

        /// <summary>
        /// Classify a highway type into a road category for material selection.
        /// Returns: "motorway", "primary", "residential", or "footpath".
        /// </summary>
        public static string ClassifyRoad(string highwayType)
        {
            string hw = (highwayType ?? "").ToLower();
            if (MotorwayTypes.Contains(hw)) return "motorway";
            if (PrimaryTypes.Contains(hw)) return "primary";
            if (FootpathTypes.Contains(hw)) return "footpath";
            return "residential"; // default
        }

        public static GameObject Build(OsmWay way, OsmData data, Material roadMaterial,
            Material sidewalkMaterial, OriginShifter originShifter, float defaultWidth = 9.0f,
            float widthScale = 1.0f)
        {
            string highwayType = (way.GetTag("highway") ?? "").ToLower();
            var matDict = new Dictionary<string, Material>
            {
                { "motorway", roadMaterial },
                { "primary", roadMaterial },
                { "residential", roadMaterial },
                { "footpath", sidewalkMaterial ?? roadMaterial }
            };
            return Build(way, data, matDict, sidewalkMaterial, originShifter, defaultWidth, true, true, widthScale);
        }

        /// <summary>
        /// Full build with road-type material dictionary.
        /// Keys: "motorway", "primary", "residential", "footpath".
        /// </summary>
        public static GameObject Build(OsmWay way, OsmData data,
            Dictionary<string, Material> roadMaterials, Material sidewalkMaterial,
            OriginShifter originShifter, float defaultWidth = 9.0f,
            bool rampAtStart = true, bool rampAtEnd = true,
            float widthScale = 1.0f)
        {
            List<Vector3> path = new List<Vector3>();

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    Vector3 pos = originShifter.GetLocalPosition(node.Latitude, node.Longitude);
                    path.Add(pos);
                }
            }

            if (path.Count < 2) return null;

            float width = DetermineWidth(way, defaultWidth) * widthScale;
            string highwayType = (way.GetTag("highway") ?? "").ToLower();
            string roadClass = ClassifyRoad(highwayType);

            // Select material based on road class
            Material roadMat = roadMaterials.ContainsKey(roadClass)
                ? roadMaterials[roadClass]
                : roadMaterials.Values.FirstOrDefault();

            // ── Check if this is a bridge ──
            bool isBridge = way.HasTag("bridge") && (way.GetTag("bridge") ?? "").ToLower() != "no";

            if (isBridge)
            {
                RoadSpatialIndex.AddRoadPath(path, width, sidewalkMaterial != null ? SIDEWALK_WIDTH : 0f);
                GameObject bridgeObj = BridgeBuilder.Build(path, width, roadMat, sidewalkMaterial, way.Id, highwayType, rampAtStart, rampAtEnd);
                if (bridgeObj != null && path.Count >= 2)
                {
                    if (rampAtStart)
                    {
                        Vector3 dirStart = (path[0] - path[1]).normalized;
                        _roadEnds.Add(new RoadEnd { Position = path[0], Direction = dirStart, Width = width, Material = roadMat, RoadClass = roadClass });
                    }
                    if (rampAtEnd)
                    {
                        Vector3 dirEnd = (path[path.Count - 1] - path[path.Count - 2]).normalized;
                        _roadEnds.Add(new RoadEnd { Position = path[path.Count - 1], Direction = dirEnd, Width = width, Material = roadMat, RoadClass = roadClass });
                    }
                }
                return bridgeObj;
            }

            // ── Apply curve smoothing ──
            // Higher subdivisions for major roads, lower for minor ones
            if (path.Count >= 3)
            {
                int subdivisions = (roadClass == "motorway" || roadClass == "primary") ? 6 : 4;
                path = GeometryUtils.SmoothPath(path, subdivisions);
            }

            // ── Track endpoints for intersection detection ──
            if (path.Count >= 2)
            {
                // First point (direction is from p1 to p0 — pointing OUT of the road)
                Vector3 dirStart = (path[0] - path[1]).normalized;
                _roadEnds.Add(new RoadEnd
                {
                    Position = path[0],
                    Direction = dirStart,
                    Width = width,
                    Material = roadMat,
                    RoadClass = roadClass
                });

                // Last point (direction is from p[n-1] to p[n] — pointing OUT of the road)
                Vector3 dirEnd = (path[path.Count - 1] - path[path.Count - 2]).normalized;
                _roadEnds.Add(new RoadEnd
                {
                    Position = path[path.Count - 1],
                    Direction = dirEnd,
                    Width = width,
                    Material = roadMat,
                    RoadClass = roadClass
                });
            }

            // ── Normal road ──
            bool isMotorway = (roadClass == "motorway");
            bool addSidewalks = sidewalkMaterial != null && width >= 4f
                && roadClass != "footpath" && !isMotorway;

            // Register road in spatial index for zero-nature-on-roads guarantee
            RoadSpatialIndex.AddRoadPath(path, width, addSidewalks ? SIDEWALK_WIDTH : 0f);

            GameObject parent = new GameObject($"Road_{way.Id}");

            GameObject road = CreateSolidStrip(path, width, ROAD_Y_SURFACE, ROAD_THICKNESS,
                roadMat, $"RoadSurface_{way.Id}");
            if (road != null) road.transform.SetParent(parent.transform);

            if (addSidewalks)
            {
                float halfRoad = width / 2f;
                List<Vector3> leftPath = new List<Vector3>();
                List<Vector3> rightPath = new List<Vector3>();

                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 forward = GetForward(path, i);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                    leftPath.Add(path[i] - right * (halfRoad + SIDEWALK_WIDTH / 2f));
                    rightPath.Add(path[i] + right * (halfRoad + SIDEWALK_WIDTH / 2f));
                }

                GameObject leftSW = CreateSolidStrip(leftPath, SIDEWALK_WIDTH, SIDEWALK_Y_SURFACE,
                    SIDEWALK_THICKNESS, sidewalkMaterial, $"SidewalkL_{way.Id}");
                GameObject rightSW = CreateSolidStrip(rightPath, SIDEWALK_WIDTH, SIDEWALK_Y_SURFACE,
                    SIDEWALK_THICKNESS, sidewalkMaterial, $"SidewalkR_{way.Id}");

                if (leftSW != null) leftSW.transform.SetParent(parent.transform);
                if (rightSW != null) rightSW.transform.SetParent(parent.transform);
            }

            return parent;
        }

        /// <summary>
        /// Backward-compatible overload (single material, no sidewalks).
        /// </summary>
        public static GameObject Build(OsmWay way, OsmData data, Material material,
            OriginShifter originShifter, float defaultWidth = 6.0f)
        {
            return Build(way, data, material, null, originShifter, defaultWidth);
        }


        // ══════════════════════════════════════════════════════════════
        //  ROAD STRIP WITH THICKNESS
        // ══════════════════════════════════════════════════════════════

        private static float DetermineWidth(OsmWay way, float defaultWidth)
        {
            if (way.Tags.ContainsKey("width"))
            {
                if (float.TryParse(way.Tags["width"].Replace("m", ""),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float w))
                    return w;
            }

            string type = (way.GetTag("highway") ?? "").ToLower();
            switch (type)
            {
                case "motorway":
                case "motorway_link":
                case "trunk":
                case "trunk_link": return 16f;
                case "primary":
                case "primary_link": return 14f;
                case "secondary":
                case "secondary_link": return 11f;
                case "tertiary":
                case "tertiary_link":
                case "residential":
                case "unclassified":
                case "living_street": return 9f;
                case "service": return 6f;
                case "footway":
                case "path":
                case "cycleway":
                case "steps":
                case "track": return 3f;
                case "pedestrian": return 6f;
                default: return defaultWidth;
            }
        }

        public static GameObject CreateSolidStrip(List<Vector3> path, float width,
            float surfaceY, float thickness, Material material, string name)
        {
            return CreateSolidStrip(path, width, surfaceY, thickness, material, name, usePathY: false);
        }

        public static GameObject CreateSolidStrip(List<Vector3> path, float width,
            float thickness, Material material, string name)
        {
            return CreateSolidStrip(path, width, 0f, thickness, material, name, usePathY: true);
        }

        public static GameObject CreateSolidStrip(List<Vector3> path, float width,
            float surfaceY, float thickness, Material material, string name, bool usePathY)
        {
            if (path == null || path.Count < 2) return null;

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float halfWidth = width / 2.0f;
            float uvY = 0;
            float uvScale = 1f / width;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                Vector3 forward = GetForward(path, i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = new Vector3(-forward.z, 0f, forward.x).normalized;
                    if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                }

                float curSurfaceY = usePathY ? current.y : surfaceY;
                float curBottomY = curSurfaceY - thickness;

                Vector3 leftTop = current - right * halfWidth;
                leftTop.y = curSurfaceY;
                Vector3 rightTop = current + right * halfWidth;
                rightTop.y = curSurfaceY;
                Vector3 leftBot = current - right * halfWidth;
                leftBot.y = curBottomY;
                Vector3 rightBot = current + right * halfWidth;
                rightBot.y = curBottomY;

                verts.Add(leftTop);   // idx + 0
                verts.Add(rightTop);  // idx + 1
                verts.Add(leftBot);   // idx + 2
                verts.Add(rightBot);  // idx + 3

                if (i > 0)
                {
                    float dist = Vector3.Distance(path[i], path[i - 1]);
                    uvY += dist * uvScale;
                }

                uvs.Add(new Vector2(0, uvY));
                uvs.Add(new Vector2(1, uvY));
                uvs.Add(new Vector2(0, uvY));
                uvs.Add(new Vector2(1, uvY));
            }

            for (int i = 0; i < path.Count - 1; i++)
            {
                int b = i * 4;
                int n = (i + 1) * 4;

                // Top surface
                tris.Add(b + 0); tris.Add(n + 0); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(n + 0); tris.Add(n + 1);

                // Left side wall
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(n + 0);
                tris.Add(n + 0); tris.Add(b + 2); tris.Add(n + 2);

                // Right side wall
                tris.Add(b + 1); tris.Add(n + 1); tris.Add(b + 3);
                tris.Add(b + 3); tris.Add(n + 1); tris.Add(n + 3);

                // Bottom surface
                tris.Add(b + 2); tris.Add(b + 3); tris.Add(n + 2);
                tris.Add(n + 2); tris.Add(b + 3); tris.Add(n + 3);
            }

            // Start cap
            tris.Add(0); tris.Add(1); tris.Add(3);
            tris.Add(0); tris.Add(3); tris.Add(2);

            // End cap
            int last = (path.Count - 1) * 4;
            tris.Add(last + 1); tris.Add(last + 0); tris.Add(last + 2);
            tris.Add(last + 1); tris.Add(last + 2); tris.Add(last + 3);

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            return go;
        }

        // ── Helpers ──

        public static Vector3 GetForward(List<Vector3> path, int i)
        {
            if (path == null || path.Count < 2) return Vector3.forward;

            Vector3 forward = Vector3.zero;
            if (i < path.Count - 1)
            {
                Vector3 dNext = path[i + 1] - path[i];
                dNext.y = 0f;
                if (dNext.sqrMagnitude > 0.0001f) forward += dNext.normalized;
            }
            if (i > 0)
            {
                Vector3 dPrev = path[i] - path[i - 1];
                dPrev.y = 0f;
                if (dPrev.sqrMagnitude > 0.0001f) forward += dPrev.normalized;
            }

            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                // Fallback to outgoing or incoming segment direction
                if (i < path.Count - 1)
                {
                    Vector3 d = path[i + 1] - path[i];
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f) forward = d.normalized;
                }
                if (forward.sqrMagnitude < 0.0001f && i > 0)
                {
                    Vector3 d = path[i] - path[i - 1];
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f) forward = d.normalized;
                }
                // Scan path forward
                if (forward.sqrMagnitude < 0.0001f)
                {
                    for (int k = i + 1; k < path.Count; k++)
                    {
                        Vector3 d = path[k] - path[i];
                        d.y = 0f;
                        if (d.sqrMagnitude > 0.0001f) { forward = d.normalized; break; }
                    }
                }
                // Scan path backward
                if (forward.sqrMagnitude < 0.0001f)
                {
                    for (int k = i - 1; k >= 0; k--)
                    {
                        Vector3 d = path[i] - path[k];
                        d.y = 0f;
                        if (d.sqrMagnitude > 0.0001f) { forward = d.normalized; break; }
                    }
                }
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            }

            forward.Normalize();
            return forward;
        }

        public static Vector3 GetPointAlongPath(List<Vector3> path, float t)
        {
            if (path.Count < 2) return path[0];
            if (t <= 0f) return path[0];
            if (t >= 1f) return path[path.Count - 1];

            float totalLen = 0f;
            for (int i = 1; i < path.Count; i++)
                totalLen += Vector3.Distance(path[i], path[i - 1]);

            float targetDist = t * totalLen;
            float accumulated = 0f;

            for (int i = 1; i < path.Count; i++)
            {
                float segLen = Vector3.Distance(path[i], path[i - 1]);
                if (accumulated + segLen >= targetDist)
                {
                    float segT = (targetDist - accumulated) / segLen;
                    return Vector3.Lerp(path[i - 1], path[i], segT);
                }
                accumulated += segLen;
            }

            return path[path.Count - 1];
        }

        // ── Road Network Builder with Intelligent Multi-Tier Flyover & Bridge Chaining ──

        public static int GetWayLayer(OsmWay way)
        {
            if (way == null) return 0;
            if (way.HasTag("layer") && int.TryParse(way.GetTag("layer"), out int l))
                return l;
            if (way.HasTag("level") && int.TryParse(way.GetTag("level"), out int lvl))
                return lvl;
            if (way.HasTag("bridge") && (way.GetTag("bridge") ?? "").ToLower() != "no")
                return 1;
            return 0;
        }

        public static float GetLayerElevation(int layer)
        {
            if (layer <= 0) return ROAD_Y_SURFACE; // 0.08f
            return layer * 6.0f; // 6m per tier / floor
        }

        private static int GetNodeMaxOtherLayer(long nodeId, OsmWay currentWay, Dictionary<long, List<OsmWay>> nodeToWays, int fallback)
        {
            if (!nodeToWays.TryGetValue(nodeId, out var ways) || ways == null) return fallback;
            int maxLayer = 0;
            bool foundOther = false;
            foreach (var w in ways)
            {
                if (w == currentWay) continue;
                int lyr = GetWayLayer(w);
                if (lyr > maxLayer) maxLayer = lyr;
                foundOther = true;
            }
            return foundOther ? maxLayer : fallback;
        }

        private class BridgeChain
        {
            public List<long> NodeIds = new List<long>();
            public List<Vector3> Path = new List<Vector3>();
            public float Width;
            public string HighwayType;
            public string RoadClass;
            public Material RoadMat;
            public long Id;
            public int Layer;
            public float StartElevation;
            public float EndElevation;
            public bool IsFullRamp;
        }

        /// <summary>
        /// Builds complete road network with multi-tier flyover elevations, smooth incline ramps,
        /// and safe bridge chaining. Prevents hairpins, needle-point spikes, and cross-tier collisions.
        /// </summary>
        public static List<GameObject> BuildRoadNetwork(
            List<OsmWay> highwayWays,
            OsmData data,
            Dictionary<string, Material> roadMaterials,
            Material sidewalkMaterial,
            OriginShifter originShifter,
            float defaultWidth = 9.0f,
            float widthScale = 1.0f)
        {
            List<GameObject> result = new List<GameObject>();
            if (highwayWays == null || highwayWays.Count == 0) return result;

            // 1. Build node-to-ways mapping
            Dictionary<long, List<OsmWay>> nodeToWays = new Dictionary<long, List<OsmWay>>();
            foreach (var way in highwayWays)
            {
                if (way.NodeIds == null || way.NodeIds.Count < 2) continue;
                string hwType = (way.GetTag("highway") ?? "").ToLower();
                if (FootpathTypes.Contains(hwType)) continue;

                foreach (long nid in way.NodeIds)
                {
                    if (!nodeToWays.TryGetValue(nid, out var list))
                    {
                        list = new List<OsmWay>(3);
                        nodeToWays[nid] = list;
                    }
                    if (!list.Contains(way)) list.Add(way);
                }
            }

            // 2. Classify ways into Elevated (Bridges, Flyovers, Multi-tier Ramps) vs Ground
            List<BridgeChain> elevatedChains = new List<BridgeChain>();
            List<OsmWay> groundWays = new List<OsmWay>();

            foreach (var way in highwayWays)
            {
                string hwType = (way.GetTag("highway") ?? "").ToLower();
                if (FootpathTypes.Contains(hwType)) continue;

                int wayLayer = GetWayLayer(way);
                bool hasBridgeTag = way.HasTag("bridge") && (way.GetTag("bridge") ?? "").ToLower() != "no";
                bool isLink = hwType.Contains("_link");

                long startN = way.NodeIds[0];
                long endN = way.NodeIds[way.NodeIds.Count - 1];

                int startLayer = wayLayer;
                int endLayer = wayLayer;

                if (isLink)
                {
                    startLayer = GetNodeMaxOtherLayer(startN, way, nodeToWays, wayLayer);
                    endLayer = GetNodeMaxOtherLayer(endN, way, nodeToWays, wayLayer);
                }
                else if (hasBridgeTag && wayLayer <= 0)
                {
                    wayLayer = 1;
                }

                float startElev = GetLayerElevation(startLayer);
                float endElev = GetLayerElevation(endLayer);

                bool isElevated = (wayLayer > 0) || hasBridgeTag || (startElev > 0.5f) || (endElev > 0.5f);

                if (!isElevated)
                {
                    groundWays.Add(way);
                    continue;
                }

                // Extract valid local path
                List<Vector3> path = new List<Vector3>();
                List<long> validNodeIds = new List<long>();
                foreach (long nid in way.NodeIds)
                {
                    if (data.Nodes.TryGetValue(nid, out OsmNode node))
                    {
                        path.Add(originShifter.GetLocalPosition(node.Latitude, node.Longitude));
                        validNodeIds.Add(nid);
                    }
                }
                if (path.Count < 2) continue;

                float width = DetermineWidth(way, defaultWidth) * widthScale;
                string roadClass = ClassifyRoad(hwType);
                Material roadMat = roadMaterials.ContainsKey(roadClass) ? roadMaterials[roadClass] : roadMaterials.Values.FirstOrDefault();

                bool isFullRamp = Mathf.Abs(startElev - endElev) > 1.0f;

                elevatedChains.Add(new BridgeChain
                {
                    NodeIds = validNodeIds,
                    Path = path,
                    Width = width,
                    HighwayType = hwType,
                    RoadClass = roadClass,
                    RoadMat = roadMat,
                    Id = way.Id,
                    Layer = wayLayer,
                    StartElevation = startElev,
                    EndElevation = endElev,
                    IsFullRamp = isFullRamp
                });
            }

            // 3. Chain contiguous bridge/flyover spans of the same layer
            // STRICT SAFETY: Only chain if degree == 2, same layer, sequential flow, smooth angle
            bool mergedAny = true;
            while (mergedAny)
            {
                mergedAny = false;
                for (int i = 0; i < elevatedChains.Count; i++)
                {
                    var c1 = elevatedChains[i];
                    if (c1.IsFullRamp) continue; // Incline ramps should not be chained into level bridges

                    for (int j = i + 1; j < elevatedChains.Count; j++)
                    {
                        var c2 = elevatedChains[j];
                        if (c2.IsFullRamp) continue;
                        if (c1.Layer != c2.Layer) continue;
                        if (Mathf.Abs(c1.Width - c2.Width) > 2.5f) continue;

                        long c1StartN = c1.NodeIds[0];
                        long c1EndN = c1.NodeIds[c1.NodeIds.Count - 1];
                        long c2StartN = c2.NodeIds[0];
                        long c2EndN = c2.NodeIds[c2.NodeIds.Count - 1];

                        // c1 -> c2 sequential continuation
                        if (c1EndN == c2StartN && nodeToWays.TryGetValue(c1EndN, out var waysAtJunc) && waysAtJunc.Count == 2)
                        {
                            Vector3 dir1 = (c1.Path[c1.Path.Count - 1] - c1.Path[c1.Path.Count - 2]).normalized;
                            Vector3 dir2 = (c2.Path[1] - c2.Path[0]).normalized;
                            dir1.y = 0; dir2.y = 0;

                            if (Vector3.Dot(dir1, dir2) > 0.25f)
                            {
                                for (int k = 1; k < c2.Path.Count; k++)
                                {
                                    c1.Path.Add(c2.Path[k]);
                                    c1.NodeIds.Add(c2.NodeIds[k]);
                                }
                                c1.EndElevation = c2.EndElevation;
                                elevatedChains.RemoveAt(j);
                                mergedAny = true;
                                break;
                            }
                        }
                        // c2 -> c1 sequential continuation
                        else if (c2EndN == c1StartN && nodeToWays.TryGetValue(c2EndN, out var waysAtJunc2) && waysAtJunc2.Count == 2)
                        {
                            Vector3 dir2 = (c2.Path[c2.Path.Count - 1] - c2.Path[c2.Path.Count - 2]).normalized;
                            Vector3 dir1 = (c1.Path[1] - c1.Path[0]).normalized;
                            dir2.y = 0; dir1.y = 0;

                            if (Vector3.Dot(dir2, dir1) > 0.25f)
                            {
                                for (int k = c1.Path.Count - 1; k >= 1; k--)
                                {
                                    c2.Path.Add(c1.Path[k]);
                                    c2.NodeIds.Add(c1.NodeIds[k]);
                                }
                                c2.EndElevation = c1.EndElevation;
                                elevatedChains[i] = c2;
                                elevatedChains.RemoveAt(j);
                                mergedAny = true;
                                break;
                            }
                        }
                    }
                    if (mergedAny) break;
                }
            }

            // 4. Build elevated chains / flyovers / ramps
            for (int i = 0; i < elevatedChains.Count; i++)
            {
                var bc = elevatedChains[i];
                float targetElev = GetLayerElevation(bc.Layer);

                // Check connectivity at endpoints
                bool connectedStart = false;
                bool connectedEnd = false;

                if (!bc.IsFullRamp)
                {
                    for (int j = 0; j < elevatedChains.Count; j++)
                    {
                        if (i == j) continue;
                        var other = elevatedChains[j];
                        if (other.Layer != bc.Layer) continue;

                        long sN = bc.NodeIds[0];
                        long eN = bc.NodeIds[bc.NodeIds.Count - 1];
                        if (other.NodeIds.Contains(sN)) connectedStart = true;
                        if (other.NodeIds.Contains(eN)) connectedEnd = true;
                    }
                }

                bool rampAtStart = !connectedStart && (bc.StartElevation < targetElev - 0.2f);
                bool rampAtEnd = !connectedEnd && (bc.EndElevation < targetElev - 0.2f);

                float sElev = bc.IsFullRamp ? bc.StartElevation : (rampAtStart ? ROAD_Y_SURFACE : targetElev);
                float eElev = bc.IsFullRamp ? bc.EndElevation : (rampAtEnd ? ROAD_Y_SURFACE : targetElev);

                RoadSpatialIndex.AddRoadPath(bc.Path, bc.Width, 0f);
                GameObject bObj = BridgeBuilder.Build(bc.Path, bc.Width, bc.RoadMat, sidewalkMaterial,
                    bc.Id, bc.HighwayType, rampAtStart, rampAtEnd, sElev, eElev, targetElev);

                if (bObj != null)
                {
                    result.Add(bObj);

                    if (rampAtStart && bc.Path.Count >= 2)
                    {
                        Vector3 dirStart = (bc.Path[0] - bc.Path[1]).normalized;
                        _roadEnds.Add(new RoadEnd { Position = bc.Path[0], Direction = dirStart, Width = bc.Width, Material = bc.RoadMat, RoadClass = bc.RoadClass });
                    }
                    if (rampAtEnd && bc.Path.Count >= 2)
                    {
                        Vector3 dirEnd = (bc.Path[bc.Path.Count - 1] - bc.Path[bc.Path.Count - 2]).normalized;
                        _roadEnds.Add(new RoadEnd { Position = bc.Path[bc.Path.Count - 1], Direction = dirEnd, Width = bc.Width, Material = bc.RoadMat, RoadClass = bc.RoadClass });
                    }
                }
            }

            // 5. Build standard ground roads
            foreach (var way in groundWays)
            {
                GameObject road = Build(way, data, roadMaterials, sidewalkMaterial, originShifter, defaultWidth, true, true, widthScale);
                if (road != null) result.Add(road);
            }

            return result;
        }
    }
}
