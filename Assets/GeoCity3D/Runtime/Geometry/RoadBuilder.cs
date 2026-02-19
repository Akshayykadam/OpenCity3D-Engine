using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class RoadBuilder
    {
        public static GameObject Build(OsmWay way, OsmData data, Material material, OriginShifter originShifter, float defaultWidth = 4.0f)
        {
            List<Vector3> path = new List<Vector3>();

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    path.Add(originShifter.GetLocalPosition(node.Latitude, node.Longitude));
                }
            }

            if (path.Count < 2) return null;

            float width = defaultWidth;
            if (way.Tags.ContainsKey("width"))
            {
                 if (float.TryParse(way.Tags["width"].Replace("m", ""), out float parsedWidth))
                {
                    width = parsedWidth;
                }
            }

            return CreateRoadMesh(path, width, material, way.Id);
        }

        private static GameObject CreateRoadMesh(List<Vector3> path, float width, Material material, long id)
        {
            GameObject go = new GameObject($"Road_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;

            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float halfWidth = width / 2.0f;

            float uvY = 0;
            float scale = 0.2f; // Adjust road texture scale

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 current = path[i];
                Vector3 forward = Vector3.zero;

                if (i < path.Count - 1)
                {
                    forward += (path[i + 1] - current).normalized;
                }
                if (i > 0)
                {
                    forward += (current - path[i - 1]).normalized;
                }
                forward.Normalize();
                
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                vertices.Add(current - right * halfWidth); // Left
                vertices.Add(current + right * halfWidth); // Right

                // Calculate Distance for V coordinate
                if (i > 0)
                {
                    float dist = Vector3.Distance(path[i], path[i - 1]);
                    uvY += dist * scale;
                }

                uvs.Add(new Vector2(0, uvY));
                uvs.Add(new Vector2(1, uvY));
            }

            for (int i = 0; i < path.Count - 1; i++)
            {
                int baseIdx = i * 2;
                
                triangles.Add(baseIdx); // Left current
                triangles.Add(baseIdx + 2); // Left next
                triangles.Add(baseIdx + 1); // Right current

                triangles.Add(baseIdx + 1); // Right current
                triangles.Add(baseIdx + 2); // Left next
                triangles.Add(baseIdx + 3); // Right next
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();

            mf.mesh = mesh;
            go.AddComponent<MeshCollider>();

            return go;
        }
    }
}
