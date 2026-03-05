using System.Collections.Generic;
using UnityEngine;

namespace GeoCity3D.Geometry
{
    /// <summary>
    /// Spawns parked vehicles along road edges using prefab models.
    /// </summary>
    public static class VehicleBuilder
    {
        /// <summary>
        /// Place parked vehicles along a road path at regular intervals.
        /// Vehicles are offset to the road edge and rotated to face the road direction.
        /// </summary>
        public static List<GameObject> PlaceParkedVehicles(
            List<Vector3> roadPath, GameObject[] vehiclePrefabs, float spacing = 30f)
        {
            List<GameObject> vehicles = new List<GameObject>();
            if (roadPath == null || roadPath.Count < 2) return vehicles;
            if (vehiclePrefabs == null || vehiclePrefabs.Length == 0) return vehicles;

            float accumulated = 0f;
            bool rightSide = true;

            for (int i = 0; i < roadPath.Count - 1; i++)
            {
                Vector3 a = roadPath[i];
                Vector3 b = roadPath[i + 1];
                float segLen = Vector3.Distance(a, b);
                Vector3 dir = (b - a).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                float pos = spacing - accumulated;
                while (pos < segLen)
                {
                    // Only place ~40% of possible spots to avoid overcrowding
                    if (Random.value > 0.4f)
                    {
                        pos += spacing;
                        rightSide = !rightSide;
                        continue;
                    }

                    Vector3 point = Vector3.Lerp(a, b, pos / segLen);
                    float offset = rightSide ? 2.5f : -2.5f;
                    Vector3 vehiclePos = point + right * offset;
                    vehiclePos.y = 0f;

                    GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];

                    // Face along road direction; flip 180° on left side so cars face "forward"
                    float yAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    if (!rightSide) yAngle += 180f;

                    GameObject vehicle = Object.Instantiate(prefab, vehiclePos,
                        Quaternion.Euler(0f, yAngle, 0f));
                    vehicle.name = $"Vehicle_{vehicles.Count}";

                    // SimplePoly vehicles may need scale adjustment
                    // FBX models are typically exported at 0.01 scale
                    Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds totalBounds = renderers[0].bounds;
                        for (int r = 1; r < renderers.Length; r++)
                            totalBounds.Encapsulate(renderers[r].bounds);

                        float maxDim = Mathf.Max(totalBounds.size.x, totalBounds.size.z);
                        // Target ~4m long vehicle
                        if (maxDim < 0.5f)
                        {
                            // Probably at FBX default scale, scale up
                            vehicle.transform.localScale = Vector3.one * (4f / Mathf.Max(maxDim, 0.01f));
                        }
                        else if (maxDim > 15f)
                        {
                            // Over-scaled, bring down
                            vehicle.transform.localScale = Vector3.one * (4f / maxDim);
                        }

                        // Ground the vehicle
                        renderers = vehicle.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            Bounds fb = renderers[0].bounds;
                            for (int r = 1; r < renderers.Length; r++)
                                fb.Encapsulate(renderers[r].bounds);
                            Vector3 p = vehicle.transform.position;
                            p.y -= fb.min.y;
                            vehicle.transform.position = p;
                        }
                    }

                    vehicles.Add(vehicle);
                    rightSide = !rightSide;
                    pos += spacing;
                }

                accumulated = segLen - (pos - spacing);
            }

            return vehicles;
        }
    }
}
