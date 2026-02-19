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
        // Stores road endpoints for intersection detection
        private static List<Vector3> _roadEndpoints = new List<Vector3>();
        private static List<float> _roadWidths = new List<float>();

        public static void ClearIntersectionData()
        {
            _roadEndpoints.Clear();
            _roadWidths.Clear();
        }

        public static List<Vector3> GetRoadEndpoints() => _roadEndpoints;
        public static List<float> GetRoadWidths() => _roadWidths;

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
            OriginShifter originShifter, float defaultWidth = 6.0f)
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

            // ── Apply curve smoothing ──
            // Higher subdivisions for major roads, lower for minor ones
            if (!isBridge && path.Count >= 3)
            {
                int subdivisions = (roadClass == "motorway" || roadClass == "primary") ? 6 : 4;
                path = GeometryUtils.SmoothPath(path, subdivisions);
            }

            // ── Track endpoints for intersection detection ──
            if (path.Count >= 2)
            {
                _roadEndpoints.Add(path[0]);
                _roadEndpoints.Add(path[path.Count - 1]);
                _roadWidths.Add(width);
                _roadWidths.Add(width);
            }

            if (isBridge)
            {
                return BuildBridge(path, width, roadMat, sidewalkMaterial, way.Id, highwayType);
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
        //  BRIDGE — elevated deck + railings + support pillars
        // ══════════════════════════════════════════════════════════════

        private static GameObject BuildBridge(List<Vector3> path, float width,
            Material roadMat, Material sidewalkMat, long id, string highwayType)
        {
            GameObject parent = new GameObject($"Bridge_{id}");

            // Elevated path
            List<Vector3> elevatedPath = new List<Vector3>();
            for (int i = 0; i < path.Count; i++)
            {
                elevatedPath.Add(new Vector3(path[i].x, BRIDGE_ELEVATION, path[i].z));
            }

            // ── Bridge deck (thick slab) ──
            GameObject deck = CreateSolidStrip(elevatedPath, width + 1f, BRIDGE_ELEVATION,
                BRIDGE_DECK_THICKNESS, roadMat, $"BridgeDeck_{id}");
            if (deck != null) deck.transform.SetParent(parent.transform);

            // ── Railings (left and right) ──
            if (sidewalkMat != null)
            {
                float halfWidth = (width + 1f) / 2f;

                for (int side = -1; side <= 1; side += 2)
                {
                    List<Vector3> railPath = new List<Vector3>();
                    for (int i = 0; i < elevatedPath.Count; i++)
                    {
                        Vector3 forward = GetForward(elevatedPath, i);
                        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                        railPath.Add(elevatedPath[i] + right * halfWidth * side);
                    }

                    string railName = side < 0 ? $"RailL_{id}" : $"RailR_{id}";
                    GameObject rail = CreateSolidStrip(railPath, BRIDGE_RAIL_THICKNESS,
                        BRIDGE_ELEVATION + BRIDGE_RAIL_HEIGHT / 2f,
                        BRIDGE_RAIL_HEIGHT, sidewalkMat, railName);
                    if (rail != null) rail.transform.SetParent(parent.transform);
                }
            }

            // ── Support pillars ──
            float totalLength = 0f;
            for (int i = 1; i < path.Count; i++)
                totalLength += Vector3.Distance(path[i], path[i - 1]);

            int pillarCount = Mathf.Max(2, Mathf.FloorToInt(totalLength / PILLAR_SPACING) + 1);

            for (int p = 0; p < pillarCount; p++)
            {
                float t = (float)p / (pillarCount - 1);
                Vector3 pos = GetPointAlongPath(path, t);
                pos.y = 0;

                GameObject pillar = CreatePillar(pos, PILLAR_WIDTH, BRIDGE_ELEVATION,
                    roadMat, $"Pillar_{id}_{p}");
                if (pillar != null) pillar.transform.SetParent(parent.transform);
            }

            return parent;
        }

        // ── Support Pillar (solid box) ──

        private static GameObject CreatePillar(Vector3 basePos, float size, float height,
            Material mat, string name)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            float half = size / 2f;
            Mesh mesh = new Mesh();

            Vector3[] verts = new Vector3[]
            {
                // Bottom face
                basePos + new Vector3(-half, 0, -half),
                basePos + new Vector3( half, 0, -half),
                basePos + new Vector3( half, 0,  half),
                basePos + new Vector3(-half, 0,  half),
                // Top face
                basePos + new Vector3(-half, height, -half),
                basePos + new Vector3( half, height, -half),
                basePos + new Vector3( half, height,  half),
                basePos + new Vector3(-half, height,  half),
            };

            int[] tris = new int[]
            {
                // Front
                0,4,1, 1,4,5,
                // Right
                1,5,2, 2,5,6,
                // Back
                2,6,3, 3,6,7,
                // Left
                3,7,0, 0,7,4,
                // Top
                4,7,5, 5,7,6,
                // Bottom
                0,1,3, 1,2,3,
            };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            return go;
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

        private static GameObject CreateSolidStrip(List<Vector3> path, float width,
            float surfaceY, float thickness, Material material, string name)
        {
            if (path.Count < 2) return null;

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float halfWidth = width / 2.0f;
            float bottomY = surfaceY - thickness;
            float uvY = 0;
            float uvScale = 1f / width;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                Vector3 forward = GetForward(path, i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                Vector3 leftTop = current - right * halfWidth;
                leftTop.y = surfaceY;
                Vector3 rightTop = current + right * halfWidth;
                rightTop.y = surfaceY;
                Vector3 leftBot = current - right * halfWidth;
                leftBot.y = bottomY;
                Vector3 rightBot = current + right * halfWidth;
                rightBot.y = bottomY;

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

        private static Vector3 GetForward(List<Vector3> path, int i)
        {
            Vector3 forward = Vector3.zero;
            if (i < path.Count - 1) forward += (path[i + 1] - path[i]).normalized;
            if (i > 0) forward += (path[i] - path[i - 1]).normalized;
            forward.y = 0;
            forward.Normalize();
            return forward;
        }

        private static Vector3 GetPointAlongPath(List<Vector3> path, float t)
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
    }
}
