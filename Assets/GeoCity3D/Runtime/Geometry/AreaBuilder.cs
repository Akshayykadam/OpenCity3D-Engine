using System.Collections.Generic;
using GeoCity3D.Data;
using GeoCity3D.Coordinates;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Builds solid area meshes (parks, water) with visible edge thickness.
    /// Creates a top surface + perimeter side walls for volumetric appearance.
    /// </summary>
    public class AreaBuilder
    {
        private const float EDGE_DEPTH = 0.10f; // Visible edge thickness

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

            if (Vector3.Distance(polygon[0], polygon[polygon.Count - 1]) < 0.1f)
                polygon.RemoveAt(polygon.Count - 1);

            if (polygon.Count < 3) return null;

            return CreateSolidArea(polygon, material, way.Id, namePrefix, yOffset);
        }

        private static GameObject CreateSolidArea(List<Vector3> polygon, Material material,
            long id, string prefix, float surfaceY)
        {
            GameObject go = new GameObject($"{prefix}_{id}");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // Compute bounds for UV
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

            // Clean duplicate points to prevent Triangulation failure
            List<Vector3> cleanPoly = new List<Vector3>();
            foreach (var p in polygon)
            {
                if (cleanPoly.Count == 0 || Vector3.Distance(cleanPoly[cleanPoly.Count - 1], p) > 0.1f)
                {
                    cleanPoly.Add(p);
                }
            }
            if (cleanPoly.Count >= 3 && Vector3.Distance(cleanPoly[0], cleanPoly[cleanPoly.Count - 1]) < 0.1f)
                cleanPoly.RemoveAt(cleanPoly.Count - 1);

            // ── Top surface ──
            int baseIdx = verts.Count;
            for (int i = 0; i < cleanPoly.Count; i++)
            {
                verts.Add(cleanPoly[i]);
                uvs.Add(new Vector2(
                    (cleanPoly[i].x - minX) / sizeX,
                    (cleanPoly[i].z - minZ) / sizeZ));
            }

            List<int> capTris = GeometryUtils.Triangulate(cleanPoly);
            if (capTris == null || capTris.Count < 3)
            {
                Debug.LogWarning($"AreaBuilder: Triangulation failed for {prefix}_{id}. The geometry is likely self-intersecting or too complex. Falling back to convex hull.");
                // Fallback to convex hull if Triangulator fails (better than an invisible lake)
                cleanPoly = GeometryUtils.GetConvexHull(cleanPoly);
                verts.RemoveRange(baseIdx, verts.Count - baseIdx);
                uvs.RemoveRange(baseIdx, uvs.Count - baseIdx);
                for (int i = 0; i < cleanPoly.Count; i++)
                {
                    verts.Add(cleanPoly[i]);
                    uvs.Add(new Vector2((cleanPoly[i].x - minX) / sizeX, (cleanPoly[i].z - minZ) / sizeZ));
                }
                capTris = GeometryUtils.Triangulate(cleanPoly);
            }

            if (capTris != null)
            {
                for (int i = 0; i < capTris.Count; i++)
                    tris.Add(baseIdx + capTris[i]);
            }

            // ── Side walls (edge thickness) ──
            float bottomY = surfaceY - EDGE_DEPTH;
            for (int i = 0; i < cleanPoly.Count; i++)
            {
                int next = (i + 1) % cleanPoly.Count;
                Vector3 p1 = cleanPoly[i];
                Vector3 p2 = cleanPoly[next];

                int bi = verts.Count;
                verts.Add(new Vector3(p1.x, surfaceY, p1.z));
                verts.Add(new Vector3(p2.x, surfaceY, p2.z));
                verts.Add(new Vector3(p2.x, bottomY, p2.z));
                verts.Add(new Vector3(p1.x, bottomY, p1.z));

                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 0));

                tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                tris.Add(bi); tris.Add(bi + 3); tris.Add(bi + 2);
            }

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;
            return go;
        }
    }
}
