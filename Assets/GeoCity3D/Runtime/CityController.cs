using UnityEngine;

namespace GeoCity3D
{
    public class CityController : MonoBehaviour
    {
        [Header("Building Materials")]
        public Material BuildingWallMaterial;
        public Material BuildingRoofMaterial;

        [Header("Road Materials")]
        public Material MotorwayMaterial;
        public Material PrimaryRoadMaterial;
        public Material ResidentialRoadMaterial;
        public Material FootpathMaterial;
        public Material CrosswalkMaterial;

        [Header("Infrastructure Materials")]
        public Material RoadMaterial;
        public Material SidewalkMaterial;
        public Material GroundMaterial;

        [Header("Area Materials")]
        public Material ParkMaterial;
        public Material WaterMaterial;

        // Backward compatibility
        public Material BuildingMaterial => BuildingWallMaterial;
    }
}

