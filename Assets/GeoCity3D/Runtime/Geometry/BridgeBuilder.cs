using System.Collections.Generic;
using GeoCity3D.Data;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Generates solid bridge meshes with elevated decks, railings, and support pillars.
    /// Extracted from RoadBuilder for modularity.
    /// </summary>
    public class BridgeBuilder
    {
        private const float BRIDGE_ELEVATION = 5.0f;    // Height above ground
        private const float BRIDGE_DECK_THICKNESS = 0.6f; // Thicker deck for bridges
        private const float BRIDGE_RAIL_HEIGHT = 1.0f;    // Side railing height
        private const float BRIDGE_RAIL_THICKNESS = 0.15f;
        private const float PILLAR_WIDTH = 0.8f;
        private const float PILLAR_SPACING = 20f;         // One pillar every 20m

        public static GameObject Build(List<Vector3> path, float width,
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
            GameObject deck = RoadBuilder.CreateSolidStrip(elevatedPath, width + 1f, BRIDGE_ELEVATION,
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
                        Vector3 forward = RoadBuilder.GetForward(elevatedPath, i);
                        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                        railPath.Add(elevatedPath[i] + right * halfWidth * side);
                    }

                    string railName = side < 0 ? $"RailL_{id}" : $"RailR_{id}";
                    GameObject rail = RoadBuilder.CreateSolidStrip(railPath, BRIDGE_RAIL_THICKNESS,
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
                Vector3 pos = RoadBuilder.GetPointAlongPath(path, t);
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
            mr.sharedMaterial = mat;
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
    }
}
