using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class RoadBuilder
    {
        private const float ROAD_Y_OFFSET = 0.05f;
        private const float SIDEWALK_Y_OFFSET = 0.12f;
        private const float SIDEWALK_WIDTH = 1.5f;

        public static GameObject Build(OsmWay way, OsmData data, Material roadMaterial,
            Material sidewalkMaterial, OriginShifter originShifter, float defaultWidth = 6.0f)
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
            bool addSidewalks = sidewalkMaterial != null && width >= 4f
                && highwayType != "footway" && highwayType != "path"
                && highwayType != "cycleway" && highwayType != "steps";

            GameObject parent = new GameObject($"Road_{way.Id}");

            // Road mesh
            GameObject road = CreateStripMesh(path, width, ROAD_Y_OFFSET, roadMaterial, $"RoadSurface_{way.Id}");
            if (road != null) road.transform.SetParent(parent.transform);

            // Sidewalks
            if (addSidewalks && sidewalkMaterial != null)
            {
                float halfRoad = width / 2f;

                // Build offset paths for left and right sidewalks
                List<Vector3> leftPath = new List<Vector3>();
                List<Vector3> rightPath = new List<Vector3>();

                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 forward = Vector3.zero;
                    if (i < path.Count - 1) forward += (path[i + 1] - path[i]).normalized;
                    if (i > 0) forward += (path[i] - path[i - 1]).normalized;
                    forward.y = 0;
                    forward.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                    leftPath.Add(path[i] - right * (halfRoad + SIDEWALK_WIDTH / 2f));
                    rightPath.Add(path[i] + right * (halfRoad + SIDEWALK_WIDTH / 2f));
                }

                GameObject leftSidewalk = CreateStripMesh(leftPath, SIDEWALK_WIDTH, SIDEWALK_Y_OFFSET, sidewalkMaterial, $"SidewalkL_{way.Id}");
                GameObject rightSidewalk = CreateStripMesh(rightPath, SIDEWALK_WIDTH, SIDEWALK_Y_OFFSET, sidewalkMaterial, $"SidewalkR_{way.Id}");

                if (leftSidewalk != null) leftSidewalk.transform.SetParent(parent.transform);
                if (rightSidewalk != null) rightSidewalk.transform.SetParent(parent.transform);
            }

            return parent;
        }

        /// <summary>
        /// Backward-compatible overload without sidewalk material.
        /// </summary>
        public static GameObject Build(OsmWay way, OsmData data, Material material, OriginShifter originShifter, float defaultWidth = 6.0f)
        {
            return Build(way, data, material, null, originShifter, defaultWidth);
        }

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

        private static GameObject CreateStripMesh(List<Vector3> path, float width, float yOffset, Material material, string name)
        {
            if (path.Count < 2) return null;

            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float halfWidth = width / 2.0f;
            float uvY = 0;
            float uvScale = 1f / width;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                current.y = yOffset;

                Vector3 forward = Vector3.zero;
                if (i < path.Count - 1) forward += (path[i + 1] - current).normalized;
                if (i > 0) forward += (current - path[i - 1]).normalized;
                forward.y = 0;
                forward.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                vertices.Add(current - right * halfWidth);
                vertices.Add(current + right * halfWidth);

                if (i > 0)
                {
                    float dist = Vector3.Distance(path[i], path[i - 1]);
                    uvY += dist * uvScale;
                }

                uvs.Add(new Vector2(0, uvY));
                uvs.Add(new Vector2(1, uvY));
            }

            for (int i = 0; i < path.Count - 1; i++)
            {
                int bi = i * 2;
                triangles.Add(bi); triangles.Add(bi + 2); triangles.Add(bi + 1);
                triangles.Add(bi + 1); triangles.Add(bi + 2); triangles.Add(bi + 3);
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();

            mf.mesh = mesh;

            return go;
        }
    }
}
