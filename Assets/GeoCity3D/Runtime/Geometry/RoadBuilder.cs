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
        private const float SIDEWALK_WIDTH = 1.5f;

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
            Material sidewalkMaterial, OriginShifter originShifter, float defaultWidth = 6.0f)
        {
            string highwayType = (way.GetTag("highway") ?? "").ToLower();
            var matDict = new Dictionary<string, Material>
            {
                { "motorway", roadMaterial },
                { "primary", roadMaterial },
                { "residential", roadMaterial },
                { "footpath", sidewalkMaterial ?? roadMaterial }
            };
            return Build(way, data, matDict, sidewalkMaterial, originShifter, defaultWidth);
        }

        /// <summary>
        /// Full build with road-type material dictionary.
        /// Keys: "motorway", "primary", "residential", "footpath".
        /// </summary>
        public static GameObject Build(OsmWay way, OsmData data,
            Dictionary<string, Material> roadMaterials, Material sidewalkMaterial,
            OriginShifter originShifter, float defaultWidth = 6.0f,
            bool rampAtStart = true, bool rampAtEnd = true)
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

            float width = DetermineWidth(way, defaultWidth);
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
            bool addSidewalks = sidewalkMaterial != null && width >= 4f
                && roadClass != "footpath";

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
                case "trunk": return 12f;
                case "primary": return 10f;
                case "secondary": return 8f;
                case "tertiary":
                case "residential": return 6f;
                case "service": return 4f;
                case "footway":
                case "path":
                case "cycleway": return 2f;
                case "pedestrian": return 4f;
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
            Vector3 forward = Vector3.zero;
            if (i < path.Count - 1) forward += (path[i + 1] - path[i]).normalized;
            if (i > 0) forward += (path[i] - path[i - 1]).normalized;
            forward.y = 0;
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

        // ── Road Network Builder with Intelligent Bridge Chaining ──

        private class BridgeChain
        {
            public List<long> NodeIds = new List<long>();
            public List<Vector3> Path = new List<Vector3>();
            public float Width;
            public string HighwayType;
            public string RoadClass;
            public Material RoadMat;
            public long Id;
        }

        /// <summary>
        /// Builds complete road network with intelligent bridge chaining and seamless continuity.
        /// Chained contiguous bridge ways avoid mid-span dips and clustered piers.
        /// </summary>
        public static List<GameObject> BuildRoadNetwork(
            List<OsmWay> highwayWays,
            OsmData data,
            Dictionary<string, Material> roadMaterials,
            Material sidewalkMaterial,
            OriginShifter originShifter,
            float defaultWidth = 6.0f)
        {
            List<GameObject> result = new List<GameObject>();
            if (highwayWays == null || highwayWays.Count == 0) return result;

            List<OsmWay> bridgeWays = new List<OsmWay>();
            List<OsmWay> groundWays = new List<OsmWay>();

            foreach (var way in highwayWays)
            {
                string hwType = (way.GetTag("highway") ?? "").ToLower();
                if (FootpathTypes.Contains(hwType)) continue;

                bool isBridge = way.HasTag("bridge") && (way.GetTag("bridge") ?? "").ToLower() != "no";
                if (isBridge) bridgeWays.Add(way);
                else groundWays.Add(way);
            }

            List<BridgeChain> chains = new List<BridgeChain>();

            foreach (var way in bridgeWays)
            {
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

                float width = DetermineWidth(way, defaultWidth);
                string hwType = (way.GetTag("highway") ?? "").ToLower();
                string roadClass = ClassifyRoad(hwType);
                Material roadMat = roadMaterials.ContainsKey(roadClass) ? roadMaterials[roadClass] : roadMaterials.Values.FirstOrDefault();

                chains.Add(new BridgeChain
                {
                    NodeIds = validNodeIds,
                    Path = path,
                    Width = width,
                    HighwayType = hwType,
                    RoadClass = roadClass,
                    RoadMat = roadMat,
                    Id = way.Id
                });
            }

            // Repeatedly chain contiguous bridge ways that share start/end nodes or endpoints
            bool mergedAny = true;
            while (mergedAny)
            {
                mergedAny = false;
                for (int i = 0; i < chains.Count; i++)
                {
                    var c1 = chains[i];
                    for (int j = i + 1; j < chains.Count; j++)
                    {
                        var c2 = chains[j];
                        if (Mathf.Abs(c1.Width - c2.Width) > 3.0f) continue;

                        long c1StartN = c1.NodeIds[0];
                        long c1EndN = c1.NodeIds[c1.NodeIds.Count - 1];
                        long c2StartN = c2.NodeIds[0];
                        long c2EndN = c2.NodeIds[c2.NodeIds.Count - 1];

                        Vector3 c1StartP = c1.Path[0];
                        Vector3 c1EndP = c1.Path[c1.Path.Count - 1];
                        Vector3 c2StartP = c2.Path[0];
                        Vector3 c2EndP = c2.Path[c2.Path.Count - 1];

                        if (c1EndN == c2StartN || Vector3.Distance(c1EndP, c2StartP) < 1.0f)
                        {
                            // c1 -> c2
                            for (int k = 1; k < c2.Path.Count; k++)
                            {
                                c1.Path.Add(c2.Path[k]);
                                c1.NodeIds.Add(c2.NodeIds[k]);
                            }
                            chains.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                        else if (c1EndN == c2EndN || Vector3.Distance(c1EndP, c2EndP) < 1.0f)
                        {
                            // c2 is reversed, append backwards
                            for (int k = c2.Path.Count - 2; k >= 0; k--)
                            {
                                c1.Path.Add(c2.Path[k]);
                                c1.NodeIds.Add(c2.NodeIds[k]);
                            }
                            chains.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                        else if (c1StartN == c2EndN || Vector3.Distance(c1StartP, c2EndP) < 1.0f)
                        {
                            // c2 -> c1
                            for (int k = c1.Path.Count - 1; k >= 1; k--)
                            {
                                c2.Path.Add(c1.Path[k]);
                                c2.NodeIds.Add(c1.NodeIds[k]);
                            }
                            chains[i] = c2;
                            chains.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                        else if (c1StartN == c2StartN || Vector3.Distance(c1StartP, c2StartP) < 1.0f)
                        {
                            // c1 reversed + c2
                            c1.Path.Reverse();
                            c1.NodeIds.Reverse();
                            for (int k = 1; k < c2.Path.Count; k++)
                            {
                                c1.Path.Add(c2.Path[k]);
                                c1.NodeIds.Add(c2.NodeIds[k]);
                            }
                            chains.RemoveAt(j);
                            mergedAny = true;
                            break;
                        }
                    }
                    if (mergedAny) break;
                }
            }

            // Build chained bridges with endpoint continuity checks
            for (int i = 0; i < chains.Count; i++)
            {
                var bc = chains[i];
                Vector3 sP = bc.Path[0];
                Vector3 eP = bc.Path[bc.Path.Count - 1];

                bool connectedStart = false;
                bool connectedEnd = false;

                for (int j = 0; j < chains.Count; j++)
                {
                    if (i == j) continue;
                    var other = chains[j];
                    if (Vector3.Distance(sP, other.Path[0]) < 1.5f || Vector3.Distance(sP, other.Path[other.Path.Count - 1]) < 1.5f)
                        connectedStart = true;
                    if (Vector3.Distance(eP, other.Path[0]) < 1.5f || Vector3.Distance(eP, other.Path[other.Path.Count - 1]) < 1.5f)
                        connectedEnd = true;
                }

                bool rampAtStart = !connectedStart;
                bool rampAtEnd = !connectedEnd;

                GameObject bObj = BridgeBuilder.Build(bc.Path, bc.Width, bc.RoadMat, sidewalkMaterial, bc.Id, bc.HighwayType, rampAtStart, rampAtEnd);
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

            // Build standard ground roads
            foreach (var way in groundWays)
            {
                GameObject road = Build(way, data, roadMaterials, sidewalkMaterial, originShifter, defaultWidth);
                if (road != null) result.Add(road);
            }

            return result;
        }
    }
}
