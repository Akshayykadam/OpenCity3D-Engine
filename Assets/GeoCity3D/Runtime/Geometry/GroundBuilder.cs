using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class GroundBuilder
    {
        /// <summary>
        /// Creates a large flat ground plane centered at origin.
        /// Placed at Y = -0.1 to avoid z-fighting with roads.
        /// </summary>
        public static GameObject Build(float radius, Material material)
        {
            GameObject go = new GameObject("Ground");
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            // Make ground slightly larger than the city radius
            float size = radius * 2.5f;
            float half = size / 2f;
            float uvTile = size / 20f; // tile every ~20m

            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-half, -0.1f, -half),
                new Vector3(-half, -0.1f,  half),
                new Vector3( half, -0.1f,  half),
                new Vector3( half, -0.1f, -half),
            };

            int[] triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0,      0),
                new Vector2(0,      uvTile),
                new Vector2(uvTile, uvTile),
                new Vector2(uvTile, 0),
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;

            mf.mesh = mesh;

            // Add a collider so raycasts can hit the ground
            go.AddComponent<MeshCollider>();

            return go;
        }
    }
}
