using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class BuildingBuilder
    {
        public static GameObject Build(OsmWay way, OsmData data, Material wallMat, Material roofMat, OriginShifter originShifter)
        {
            List<Vector3> footprint = new List<Vector3>();

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

            return CreateMesh(footprint, height, wallMat, roofMat, way.Id);
        }

        private static GameObject CreateMesh(List<Vector3> footprint, float height, Material wallMat, Material roofMat, long id)
        {
            GameObject go = new GameObject($"Building_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            
            // Assign submesh materials
            mr.materials = new Material[] { wallMat, roofMat };

            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            
            // Separate triangles logic
            List<int> wallTriangles = new List<int>();
            List<int> roofTrianglesIndices = new List<int>();

            // --- Walls Generation ---
            float currentDist = 0f;
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 p1 = footprint[i];
                Vector3 p2 = footprint[(i + 1) % footprint.Count];
                
                float segmentDist = Vector3.Distance(p1, p2);

                Vector3 p3 = p2 + Vector3.up * height;
                Vector3 p4 = p1 + Vector3.up * height;

                int baseIdx = vertices.Count;

                vertices.Add(p1);
                vertices.Add(p2);
                vertices.Add(p3);
                vertices.Add(p4);

                // Wall UVs (World Space Tiling)
                // u = horizontal distance, v = height
                float scale = 0.5f; // Adjust texture density
                
                uvs.Add(new Vector2(currentDist * scale, 0));
                uvs.Add(new Vector2((currentDist + segmentDist) * scale, 0));
                uvs.Add(new Vector2((currentDist + segmentDist) * scale, height * scale));
                uvs.Add(new Vector2(currentDist * scale, height * scale));

                currentDist += segmentDist;

                wallTriangles.Add(baseIdx + 0);
                wallTriangles.Add(baseIdx + 2);
                wallTriangles.Add(baseIdx + 1);

                wallTriangles.Add(baseIdx + 0);
                wallTriangles.Add(baseIdx + 3);
                wallTriangles.Add(baseIdx + 2);
            }

            // --- Roof Generation ---
            int roofBaseIndex = vertices.Count;
            // Roof vertices (flat top)
            for (int i = 0; i < footprint.Count; i++)
            {
                Vector3 v = footprint[i] + Vector3.up * height;
                vertices.Add(v);
                
                // Roof UVs (Planar mapping)
                uvs.Add(new Vector2(v.x * 0.2f, v.z * 0.2f));
            }

            List<int> roofTris = GeometryUtils.Triangulate(footprint);
            foreach (int idx in roofTris)
            {
                roofTrianglesIndices.Add(roofBaseIndex + idx);
            }

            // --- Mesh Construction ---
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wallTriangles, 0);
            mesh.SetTriangles(roofTrianglesIndices, 1);
            
            mesh.RecalculateNormals();
            
            mf.mesh = mesh;
            go.AddComponent<BoxCollider>().center = new Vector3(0, height/2, 0); // Simplified collider

            return go;
        }
    }
}
