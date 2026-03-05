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

        [Header("Tree Prefabs (Prefab Mode)")]
        public GameObject[] TreePrefabs;

        [Header("Street Prop Prefabs")]
        public GameObject[] StreetLightPrefabs;
        public GameObject[] TrafficSignalPrefabs;
        public GameObject[] StreetPropPrefabs;

        [Header("Vehicle Prefabs")]
        public GameObject[] VehiclePrefabs;

        [Header("Nature Prefabs (Parks)")]
        public GameObject[] BushPrefabs;
        public GameObject[] RockPrefabs;

        // Backward compatibility
        public Material BuildingMaterial => BuildingWallMaterial;
    }
}

