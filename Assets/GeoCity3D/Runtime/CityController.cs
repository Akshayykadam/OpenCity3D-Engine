using UnityEngine;

namespace GeoCity3D
{
    public enum BuildingMode
    {
        Procedural,  // Exact OSM footprint geometry with solid colors
        Prefab       // FBX models scaled to fit lots
    }

    public class CityController : MonoBehaviour
    {
        [Header("Generation Mode")]
        [Tooltip("Procedural = exact footprint geometry with solid colors. Prefab = FBX models scaled to fit.")]
        public BuildingMode BuildingGenerationMode = BuildingMode.Procedural;

        [Header("Building Models (Prefab Mode)")]
        public GameObject[] BuildingPrefabs;

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

