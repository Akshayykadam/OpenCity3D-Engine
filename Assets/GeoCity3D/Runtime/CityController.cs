using UnityEngine;

namespace GeoCity3D
{
    public class CityController : MonoBehaviour
    {
        [Header("Materials")]
        public Material BuildingWallMaterial;
        public Material BuildingRoofMaterial;
        public Material RoadMaterial;
        
        // Backward compatibility properties (optional, or just remove)
        public Material BuildingMaterial => BuildingWallMaterial;
    }
}
