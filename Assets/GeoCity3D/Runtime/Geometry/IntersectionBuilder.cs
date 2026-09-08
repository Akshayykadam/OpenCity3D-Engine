using System.Collections.Generic;
using UnityEngine;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Builds seamless road intersection junctions and corner curbs.
    /// Eliminates the old bug where solid sidewalk slabs covered the entire roadway.
    /// Handles Crossroads, T-Junctions, and Multi-way intersections cleanly.
    /// </summary>
    public static class IntersectionBuilder
    {
        public struct RoadBranch
        {
            public Vector3 CenterPoint;  // Trimming point along branch
            public Vector3 Direction;    // Outward direction pointing away from intersection
            public Vector3 Right;        // Right perpendicular to Direction
            public float HalfWidth;      // Half road width
            public float SidewalkWidth;  // Sidewalk width
            public Material RoadMat;
            public Material SidewalkMat;
            public float Angle;          // Radial angle in XZ plane
        }

        public class JunctionData
        {
            public Vector3 Center;
            public List<RoadBranch> Branches = new List<RoadBranch>();
            public Material BestRoadMat;
            public Material BestSidewalkMat;
            public float MaxWidth;
            public float Elevation = 0.08f;
            public int Layer = 0;
            public bool IsHighway = false;
        }

        /// <summary>
        /// Detects all intersections from OSM road ways using shared nodes, grouped by elevation layer.
        /// </summary>
        public static List<JunctionData> DetectIntersections(
            List<OsmWay> highwayWays,
            OsmData data,
            OriginShifter shifter,
            Dictionary<string, Material> roadMaterials,
            Material defaultRoadMat,
            Material defaultSidewalkMat,
            float widthScale = 1.0f)
        {
            List<JunctionData> junctions = new List<JunctionData>();
            if (highwayWays == null || highwayWays.Count == 0 || data == null) return junctions;

            // 1. Map all nodes to the highway ways that contain them
            Dictionary<long, List<OsmWay>> nodeToWays = new Dictionary<long, List<OsmWay>>();
            foreach (var way in highwayWays)
            {
                if (way.NodeIds == null || way.NodeIds.Count < 2) continue;
                string hw = (way.GetTag("highway") ?? "").ToLower();
                if (hw == "footway" || hw == "path" || hw == "steps" || hw == "cycleway") continue;

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

            HashSet<long> processedNodes = new HashSet<long>();

            foreach (var kvp in nodeToWays)
            {
                long nodeId = kvp.Key;
                List<OsmWay> connectingWays = kvp.Value;

                if (connectingWays.Count < 2) continue;
                if (!data.Nodes.TryGetValue(nodeId, out OsmNode centerNode)) continue;
                if (processedNodes.Contains(nodeId)) continue;
                processedNodes.Add(nodeId);

                Vector3 baseCenterPos = shifter.GetLocalPosition(centerNode.Latitude, centerNode.Longitude);

                // Group connecting ways by layer so elevated flyovers don't mix with ground roads
                Dictionary<int, List<OsmWay>> layerToWays = new Dictionary<int, List<OsmWay>>();
                foreach (var way in connectingWays)
                {
                    int lyr = RoadBuilder.GetWayLayer(way);
                    if (!layerToWays.TryGetValue(lyr, out var wList))
                    {
                        wList = new List<OsmWay>();
                        layerToWays[lyr] = wList;
                    }
                    wList.Add(way);
                }

                foreach (var layerKvp in layerToWays)
                {
                    int layer = layerKvp.Key;
                    List<OsmWay> layerWays = layerKvp.Value;
                    if (layerWays.Count < 2) continue;

                    float layerElev = RoadBuilder.GetLayerElevation(layer);
                    Vector3 centerPos = new Vector3(baseCenterPos.x, layerElev, baseCenterPos.z);

                    JunctionData junc = new JunctionData
                    {
                        Center = centerPos,
                        Elevation = layerElev,
                        Layer = layer
                    };

                    float maxWidth = 6f * widthScale;
                    bool hasHighway = false;

                    foreach (var way in layerWays)
                    {
                        int nodeIdx = way.NodeIds.IndexOf(nodeId);
                        if (nodeIdx < 0) continue;

                        float roadWidth = DetermineWidth(way) * widthScale;
                        if (roadWidth > maxWidth) maxWidth = roadWidth;

                        string hw = (way.GetTag("highway") ?? "").ToLower();
                        string roadClass = RoadBuilder.ClassifyRoad(hw);
                        if (roadClass == "motorway" || hw.Contains("_link")) hasHighway = true;

                        Material rMat = (roadMaterials != null && roadMaterials.TryGetValue(roadClass, out var rm) && rm != null)
                            ? rm : defaultRoadMat;

                        if (nodeIdx > 0)
                        {
                            if (data.Nodes.TryGetValue(way.NodeIds[nodeIdx - 1], out OsmNode prevNode))
                            {
                                Vector3 prevPos = shifter.GetLocalPosition(prevNode.Latitude, prevNode.Longitude);
                                Vector3 dir = (prevPos - centerPos).normalized;
                                dir.y = 0;
                                if (dir.sqrMagnitude > 0.01f)
                                {
                                    AddBranch(junc, centerPos, dir.normalized, roadWidth, rMat, defaultSidewalkMat);
                                }
                            }
                        }

                        if (nodeIdx < way.NodeIds.Count - 1)
                        {
                            if (data.Nodes.TryGetValue(way.NodeIds[nodeIdx + 1], out OsmNode nextNode))
                            {
                                Vector3 nextPos = shifter.GetLocalPosition(nextNode.Latitude, nextNode.Longitude);
                                Vector3 dir = (nextPos - centerPos).normalized;
                                dir.y = 0;
                                if (dir.sqrMagnitude > 0.01f)
                                {
                                    AddBranch(junc, centerPos, dir.normalized, roadWidth, rMat, defaultSidewalkMat);
                                }
                            }
                        }
                    }

                    FilterDuplicateBranches(junc);

                    if (junc.Branches.Count >= 2)
                    {
                        junc.MaxWidth = maxWidth;
                        junc.IsHighway = hasHighway;
                        junc.BestRoadMat = defaultRoadMat;
                        junc.BestSidewalkMat = defaultSidewalkMat;
                        junctions.Add(junc);
                    }
                }
            }

            return junctions;
        }

        private static void AddBranch(JunctionData junc, Vector3 center, Vector3 dir, float roadWidth, Material rMat, Material swMat)
        {
            float angle = Mathf.Atan2(dir.z, dir.x);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            junc.Branches.Add(new RoadBranch
            {
                Direction = dir,
                Right = right,
                HalfWidth = roadWidth * 0.5f,
                SidewalkWidth = 2.0f,
                RoadMat = rMat,
                SidewalkMat = swMat,
                Angle = angle
            });
        }

        private static void FilterDuplicateBranches(JunctionData junc)
        {
            if (junc.Branches.Count <= 1) return;

            // Sort branches radially counter-clockwise
            junc.Branches.Sort((a, b) => a.Angle.CompareTo(b.Angle));

            List<RoadBranch> filtered = new List<RoadBranch>();
            for (int i = 0; i < junc.Branches.Count; i++)
            {
                var cur = junc.Branches[i];
                bool duplicate = false;
                for (int j = 0; j < filtered.Count; j++)
                {
                    if (Vector3.Dot(cur.Direction, filtered[j].Direction) > 0.95f)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) filtered.Add(cur);
            }

            junc.Branches = filtered;
        }

        /// <summary>
        /// Builds the asphalt road junction mesh and perimeter corner curbs.
        /// </summary>
        public static GameObject BuildJunction(JunctionData junc, int index, Transform parent)
        {
            if (junc == null || junc.Branches.Count < 2) return null;

            int branchCount = junc.Branches.Count;
            float maxHalfWidth = junc.MaxWidth * 0.5f;
            float junctionRadius = maxHalfWidth + 2.5f;

            // Register with RoadSpatialIndex so nature/grass NEVER spawns on the junction
            RoadSpatialIndex.AddIntersection(junc.Center, junctionRadius + 2.0f);

            GameObject junctionRoot = new GameObject($"Junction_{index}");
            junctionRoot.transform.SetParent(parent, false);

            // ── 1. Asphalt Road Junction Polygon (Flush at deck elevation) ──
            List<Vector3> roadPoly = new List<Vector3>();
            float roadY = junc.Elevation + 0.005f;

            for (int i = 0; i < branchCount; i++)
            {
                var b = junc.Branches[i];
                float armDist = Mathf.Max(b.HalfWidth + 1.5f, junctionRadius * 0.85f);
                Vector3 armCenter = junc.Center + b.Direction * armDist;

                Vector3 leftCorner = armCenter - b.Right * b.HalfWidth;
                Vector3 rightCorner = armCenter + b.Right * b.HalfWidth;
                leftCorner.y = roadY;
                rightCorner.y = roadY;

                roadPoly.Add(leftCorner);
                roadPoly.Add(rightCorner);
            }

            // Build convex hull around corners for clean polygon triangulation
            roadPoly = GeometryUtils.GetConvexHull(roadPoly);

            if (roadPoly.Count >= 3)
            {
                GameObject roadMeshObj = CreatePolygonObject(roadPoly, junc.BestRoadMat, $"Road_Junction_{index}");
                if (roadMeshObj != null) roadMeshObj.transform.SetParent(junctionRoot.transform, false);
            }

            // ── 2. Sidewalk Corner Curbs (Only for urban ground roads, NOT for motorways/flyovers) ──
            bool addCurbs = junc.BestSidewalkMat != null && !junc.IsHighway && junc.Elevation <= 0.5f;
            if (addCurbs)
            {
                float curbY = junc.Elevation + 0.10f;
                for (int i = 0; i < branchCount; i++)
                {
                    var b1 = junc.Branches[i];
                    var b2 = junc.Branches[(i + 1) % branchCount];

                    // Angle check: avoid inverted curbs if angle between branches is too sharp
                    float dot = Vector3.Dot(b1.Direction, b2.Direction);
                    if (dot > 0.98f) continue; // Same direction

                    float armDist1 = Mathf.Max(b1.HalfWidth + 1.5f, junctionRadius * 0.85f);
                    float armDist2 = Mathf.Max(b2.HalfWidth + 1.5f, junctionRadius * 0.85f);

                    Vector3 p1Road = junc.Center + b1.Direction * armDist1 + b1.Right * b1.HalfWidth;
                    Vector3 p1SW = p1Road + b1.Right * b1.SidewalkWidth;

                    Vector3 p2Road = junc.Center + b2.Direction * armDist2 - b2.Right * b2.HalfWidth;
                    Vector3 p2SW = p2Road - b2.Right * b2.SidewalkWidth;

                    // Approximate curb corner intersection point
                    Vector3 cornerOuter = (p1SW + p2SW) * 0.5f;
                    Vector3 cornerInner = (p1Road + p2Road) * 0.5f;

                    // Create sidewalk corner wedge
                    List<Vector3> swCornerPoly = new List<Vector3>
                    {
                        new Vector3(p1Road.x, curbY, p1Road.z),
                        new Vector3(p1SW.x, curbY, p1SW.z),
                        new Vector3(cornerOuter.x, curbY, cornerOuter.z),
                        new Vector3(p2SW.x, curbY, p2SW.z),
                        new Vector3(p2Road.x, curbY, p2Road.z),
                        new Vector3(cornerInner.x, curbY, cornerInner.z)
                    };

                    swCornerPoly = GeometryUtils.GetConvexHull(swCornerPoly);
                    if (swCornerPoly.Count >= 3)
                    {
                        GameObject swObj = CreatePolygonObject(swCornerPoly, junc.BestSidewalkMat, $"Curb_{index}_{i}");
                        if (swObj != null) swObj.transform.SetParent(junctionRoot.transform, false);
                    }
                }
            }

            return junctionRoot;
        }

        private static GameObject CreatePolygonObject(List<Vector3> points, Material mat, string name)
        {
            if (points.Count < 3) return null;

            List<Vector3> clean = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                bool dup = false;
                for (int j = 0; j < clean.Count; j++)
                {
                    if (Vector3.Distance(points[i], clean[j]) < 0.05f) { dup = true; break; }
                }
                if (!dup) clean.Add(points[i]);
            }
            if (clean.Count < 3) return null;

            List<int> tris = GeometryUtils.Triangulate(clean);
            if (tris == null || tris.Count < 3) return null;

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh m = new Mesh();
            m.SetVertices(clean);
            m.SetTriangles(tris, 0);

            // Planar UVs
            List<Vector2> uvs = new List<Vector2>(clean.Count);
            for (int i = 0; i < clean.Count; i++)
            {
                uvs.Add(new Vector2(clean[i].x * 0.15f, clean[i].z * 0.15f));
            }
            m.SetUVs(0, uvs);
            m.RecalculateNormals();

            mf.sharedMesh = m;
            return go;
        }

        private static float DetermineWidth(OsmWay way)
        {
            if (way.Tags.ContainsKey("width") && float.TryParse(way.Tags["width"].Replace("m", ""),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float w))
                return w;

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
                default: return 9f;
            }
        }
    }
}
