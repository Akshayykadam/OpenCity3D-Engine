using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Builds flat polygon meshes for parks, water bodies, and other area features.
    /// </summary>
    public class AreaBuilder
    {
        public static GameObject Build(OsmWay way, OsmData data, Material material,
            OriginShifter originShifter, float yOffset, string namePrefix)
        {
            List<Vector3> polygon = new List<Vector3>();

            foreach (long nodeId in way.NodeIds)
            {
                if (data.Nodes.TryGetValue(nodeId, out OsmNode node))
                {
                    Vector3 pos = originShifter.GetLocalPosition(node.Latitude, node.Longitude);
                    pos.y = yOffset;
                    polygon.Add(pos);
                }
            }

            if (polygon.Count < 3) return null;

            // Remove duplicate closing point
            if (Vector3.Distance(polygon[0], polygon[polygon.Count - 1]) < 0.1f)
            {
                polygon.RemoveAt(polygon.Count - 1);
            }

            if (polygon.Count < 3) return null;

            return CreateAreaMesh(polygon, material, way.Id, namePrefix);
        }

        private static GameObject CreateAreaMesh(List<Vector3> polygon, Material material, long id, string prefix)
        {
            GameObject go = new GameObject($"{prefix}_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // Compute bounding box for UV mapping
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                if (polygon[i].x < minX) minX = polygon[i].x;
                if (polygon[i].x > maxX) maxX = polygon[i].x;
                if (polygon[i].z < minZ) minZ = polygon[i].z;
                if (polygon[i].z > maxZ) maxZ = polygon[i].z;
            }
            float sizeX = Mathf.Max(maxX - minX, 0.01f);
            float sizeZ = Mathf.Max(maxZ - minZ, 0.01f);

            // Scale UVs so texture tiles every ~20m
            float uvScaleX = sizeX / 20f;
            float uvScaleZ = sizeZ / 20f;

            for (int i = 0; i < polygon.Count; i++)
            {
                vertices.Add(polygon[i]);
                uvs.Add(new Vector2(
                    ((polygon[i].x - minX) / sizeX) * uvScaleX,
                    ((polygon[i].z - minZ) / sizeZ) * uvScaleZ
                ));
            }

            List<int> triangles = GeometryUtils.Triangulate(polygon);

            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            mf.mesh = mesh;

            return go;
        }
    }
}
