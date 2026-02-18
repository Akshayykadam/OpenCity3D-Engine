using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class BuildingBuilder
    {
        public static GameObject Build(OsmWay way, OsmData data, Material material, OriginShifter originShifter)
        {
            List<Vector3> footprint = new List<Vector3>();
            Vector2d center = Vector2d.zero;

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    Vector3 localPos = originShifter.GetLocalPosition(node.Latitude, node.Longitude);
                    footprint.Add(localPos);
                }
            }

            if (footprint.Count < 3) return null;

            // Remove last point if it duplicates first (closed loop)
            if (Vector3.Distance(footprint[0], footprint[footprint.Count - 1]) < 0.1f)
            {
                footprint.RemoveAt(footprint.Count - 1);
            }

            // Determine height
            float height = 10.0f; // Default height
            if (way.Tags.ContainsKey("height"))
            {
                if (float.TryParse(way.Tags["height"].Replace("m", ""), out float parsedHeight))
                {
                    height = parsedHeight;
                }
            }
            else if (way.Tags.ContainsKey("building:levels"))
            {
                if (int.TryParse(way.Tags["building:levels"], out int levels))
                {
                    height = levels * 3.0f; // Approx 3m per floor
                }
            }

            return CreateMesh(footprint, height, material, way.Id);
        }

        private static GameObject CreateMesh(List<Vector3> footprint, float height, Material material, long id)
        {
            GameObject go = new GameObject($"Building_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;

            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            // Floor
            int groundBaseIndex = 0;
            vertices.AddRange(footprint);
            
            // Roof
            int roofBaseIndex = vertices.Count;
            foreach (var v in footprint)
            {
                vertices.Add(v + Vector3.up * height);
            }

            // Triangulate Roof
            List<int> roofTriangles = GeometryUtils.Triangulate(footprint);
            foreach (int idx in roofTriangles)
            {
                triangles.Add(roofBaseIndex + idx);
            }

            // Walls
            int wallBaseIndex = vertices.Count;
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 v1 = footprint[i];
                Vector3 v2 = footprint[(i + 1) % footprint.Count];
                Vector3 v3 = v2 + Vector3.up * height;
                Vector3 v4 = v1 + Vector3.up * height;

                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);
                vertices.Add(v4);

                triangles.Add(wallBaseIndex + 0);
                triangles.Add(wallBaseIndex + 2);
                triangles.Add(wallBaseIndex + 1);

                triangles.Add(wallBaseIndex + 0);
                triangles.Add(wallBaseIndex + 3);
                triangles.Add(wallBaseIndex + 2);
                
                wallBaseIndex += 4;
            }
            
            // UVs (Simple box projection or uniform)
             for (int i = 0; i < vertices.Count; i++)
            {
                uvs.Add(new Vector2(vertices[i].x, vertices[i].z));
            }


            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            
            mf.mesh = mesh;
            go.AddComponent<BoxCollider>().center = new Vector3(0, height/2, 0); // Simplified collider

            return go;
        }
    }
}
