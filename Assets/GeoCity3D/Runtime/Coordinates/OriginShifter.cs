using UnityEngine;

namespace GeoCity3D.Coordinates
{
    /// <summary>
    /// Handles the local origin of the generated city to avoid floating point precision issues.
    /// </summary>
    public class OriginShifter : MonoBehaviour
    {
        public static OriginShifter Instance { get; private set; }

        public Vector2d WorldOrigin { get; private set; }
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetOrigin(double lat, double lon)
        {
            WorldOrigin = GeoConverter.LatLonToMeters(lat, lon);
            IsInitialized = true;
            Debug.Log($"Origin set to Lat: {lat}, Lon: {lon} (Meters: {WorldOrigin.x}, {WorldOrigin.y})");
        }

        public Vector3 GetLocalPosition(double lat, double lon)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("OriginShifter not initialized! Setting origin to target coordinate.");
                SetOrigin(lat, lon);
            }

            Vector2d meters = GeoConverter.LatLonToMeters(lat, lon);
            Vector2d localOffset = meters - WorldOrigin;

            // Mapping: x -> x (East), y -> z (North)
            return new Vector3((float)localOffset.x, 0, (float)localOffset.y);
        }
    }
}
