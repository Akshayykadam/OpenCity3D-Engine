using UnityEngine;

namespace GeoCity3D.Geometry
{
    public class GroundBuilder
    {
        /// <summary>
        /// Creates a raised platform base like an architectural model.
        /// Flat top surface with visible side walls.
        /// </summary>
        public static GameObject Build(float radius, Material topMaterial, Material sideMaterial = null)
        {
            GameObject go = new GameObject("Ground");
            
            float size = radius * 3.0f;
            float half = size / 2f;
            float platformHeight = radius * 0.04f; // Proportional height
            platformHeight = Mathf.Clamp(platformHeight, 3f, 25f);

            // ── Top surface ──
            GameObject top = CreateQuad("GroundTop", half, 0f, topMaterial);
            top.transform.SetParent(go.transform, false);

            // ── Side walls (4 sides) ──
            Material sMat = sideMaterial ?? topMaterial;

            // Front (negative Z)
            CreateSideWall("Side_Front", go.transform, sMat,
                new Vector3(-half, 0, -half), new Vector3(half, 0, -half),
                new Vector3(half, -platformHeight, -half), new Vector3(-half, -platformHeight, -half));

            // Back (positive Z)
            CreateSideWall("Side_Back", go.transform, sMat,
                new Vector3(half, 0, half), new Vector3(-half, 0, half),
                new Vector3(-half, -platformHeight, half), new Vector3(half, -platformHeight, half));

            // Left (negative X)
            CreateSideWall("Side_Left", go.transform, sMat,
                new Vector3(-half, 0, half), new Vector3(-half, 0, -half),
                new Vector3(-half, -platformHeight, -half), new Vector3(-half, -platformHeight, half));

            // Right (positive X)
            CreateSideWall("Side_Right", go.transform, sMat,
                new Vector3(half, 0, -half), new Vector3(half, 0, half),
                new Vector3(half, -platformHeight, half), new Vector3(half, -platformHeight, -half));

            // ── Bottom cap ──
            GameObject bottom = CreateQuad("GroundBottom", half, -platformHeight, topMaterial);
            bottom.transform.SetParent(go.transform, false);
            // Flip bottom face
            Mesh bMesh = bottom.GetComponent<MeshFilter>().sharedMesh;
            int[] tris = bMesh.triangles;
            System.Array.Reverse(tris);
            bMesh.triangles = tris;
            bMesh.RecalculateNormals();

            go.AddComponent<BoxCollider>();

            return go;
        }

        /// <summary>
        /// Backward-compatible single-material overload.
        /// </summary>
        public static GameObject Build(float radius, Material material)
        {
            return Build(radius, material, null);
        }

        private static GameObject CreateQuad(string name, float half, float y, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-half, y, -half),
                new Vector3(-half, y,  half),
                new Vector3( half, y,  half),
                new Vector3( half, y, -half),
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(1, 0)
            };

            mf.sharedMesh = mesh;
            return go;
        }

        private static void CreateSideWall(string name, Transform parent, Material mat,
            Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] { tl, tr, br, bl };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(1, 0), new Vector2(0, 0)
            };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
        }
    }
}
