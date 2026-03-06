using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Detects large empty spaces between buildings and fills them with
    /// trees, bushes, rocks, and small parks for a denser urban feel.
    /// </summary>
    public static class LotFiller
    {
        /// <summary>
        /// Scans the city area for empty lots and fills them with vegetation.
        /// </summary>
        /// <param name="cityBounds">The total bounds of the generated city.</param>
        /// <param name="buildingBounds">Bounds of all placed buildings.</param>
        /// <param name="roadBounds">Bounds of all placed roads.</param>
        /// <param name="treePrefabs">Tree prefabs to scatter.</param>
        /// <param name="bushPrefabs">Bush prefabs to scatter.</param>
        /// <param name="rockPrefabs">Rock prefabs to scatter.</param>
        /// <param name="parent">Parent transform for spawned objects.</param>
        /// <param name="cellSize">Grid cell size in meters (default 12m).</param>
        /// <returns>Number of vegetation objects placed.</returns>
        public static int FillEmptyLots(
            Bounds cityBounds,
            List<Bounds> buildingBounds,
            List<Bounds> roadBounds,
            GameObject[] treePrefabs,
            GameObject[] bushPrefabs,
            GameObject[] rockPrefabs,
            Transform parent,
            float cellSize = 8f)
        {
            if (treePrefabs == null || treePrefabs.Length == 0) return 0;

            int placedCount = 0;

            // Calculate grid dimensions
            float startX = cityBounds.min.x;
            float startZ = cityBounds.min.z;
            float endX = cityBounds.max.x;
            float endZ = cityBounds.max.z;

            int gridW = Mathf.CeilToInt((endX - startX) / cellSize);
            int gridH = Mathf.CeilToInt((endZ - startZ) / cellSize);

            // Create occupancy grid: true = occupied, false = empty
            bool[,] occupied = new bool[gridW, gridH];

            // Mark cells occupied by buildings
            foreach (var b in buildingBounds)
            {
                MarkBoundsOnGrid(occupied, b, startX, startZ, cellSize, gridW, gridH);
            }

            // Mark cells occupied by roads
            if (roadBounds != null)
            {
                foreach (var r in roadBounds)
                {
                    // Expand road bounds slightly to include sidewalks
                    Bounds expanded = r;
                    expanded.Expand(new Vector3(4f, 0f, 4f));
                    MarkBoundsOnGrid(occupied, expanded, startX, startZ, cellSize, gridW, gridH);
                }
            }

            // Scan for empty cells and fill them
            for (int gx = 0; gx < gridW; gx++)
            {
                for (int gz = 0; gz < gridH; gz++)
                {
                    if (occupied[gx, gz]) continue;

                    // Fill 70% of empty cells for dense vegetation
                    if (Random.value > 0.70f) continue;

                    float worldX = startX + (gx + 0.5f) * cellSize;
                    float worldZ = startZ + (gz + 0.5f) * cellSize;

                    // Add some random offset within the cell
                    worldX += Random.Range(-cellSize * 0.3f, cellSize * 0.3f);
                    worldZ += Random.Range(-cellSize * 0.3f, cellSize * 0.3f);

                    Vector3 pos = new Vector3(worldX, 0f, worldZ);

                    // Decide what to place: 75% trees, 15% bushes, 10% rocks
                    float roll = Random.value;
                    GameObject prefab;

                    if (roll < 0.75f && treePrefabs.Length > 0)
                    {
                        prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                    }
                    else if (roll < 0.85f && bushPrefabs != null && bushPrefabs.Length > 0)
                    {
                        prefab = bushPrefabs[Random.Range(0, bushPrefabs.Length)];
                    }
                    else if (rockPrefabs != null && rockPrefabs.Length > 0)
                    {
                        prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
                    }
                    else
                    {
                        prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
                    }

                    float yRot = Random.Range(0f, 360f);
                    GameObject obj = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yRot, 0f));
                    obj.name = $"LotFill_{placedCount}";

                    // Ground the object
                    GroundObject(obj);

                    obj.transform.SetParent(parent, true);
                    placedCount++;
                }
            }

            return placedCount;
        }

        /// <summary>
        /// Marks grid cells that overlap with the given bounds as occupied.
        /// </summary>
        private static void MarkBoundsOnGrid(
            bool[,] grid, Bounds b,
            float startX, float startZ, float cellSize,
            int gridW, int gridH)
        {
            int minGX = Mathf.FloorToInt((b.min.x - startX) / cellSize);
            int maxGX = Mathf.CeilToInt((b.max.x - startX) / cellSize);
            int minGZ = Mathf.FloorToInt((b.min.z - startZ) / cellSize);
            int maxGZ = Mathf.CeilToInt((b.max.z - startZ) / cellSize);

            minGX = Mathf.Clamp(minGX, 0, gridW - 1);
            maxGX = Mathf.Clamp(maxGX, 0, gridW - 1);
            minGZ = Mathf.Clamp(minGZ, 0, gridH - 1);
            maxGZ = Mathf.Clamp(maxGZ, 0, gridH - 1);

            for (int x = minGX; x <= maxGX; x++)
                for (int z = minGZ; z <= maxGZ; z++)
                    grid[x, z] = true;
        }

        /// <summary>
        /// Shifts object so its bottom sits at y=0.
        /// </summary>
        private static void GroundObject(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds fb = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    fb.Encapsulate(renderers[i].bounds);
                Vector3 pos = obj.transform.position;
                pos.y -= fb.min.y;
                obj.transform.position = pos;
            }
        }
    }
}
