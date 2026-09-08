using UnityEngine;

namespace GeoCity3D
{
    public enum BuildingMode
    {
        Procedural,  // Exact OSM footprint geometry with solid colors
        Prefab       // FBX models scaled to fit lots
    }

    public enum NatureMode
    {
        ProceduralMesh,  // Direct custom procedural meshes from real GIS data
        Prefab           // FBX prefab models
    }

    public class CityController : MonoBehaviour
    {
        [Header("Generation Modes")]
        [Tooltip("Procedural = exact footprint geometry with solid colors. Prefab = FBX models scaled to fit.")]
        public BuildingMode BuildingGenerationMode = BuildingMode.Procedural;

        [Tooltip("ProceduralMesh = generates custom low-poly tree & rock meshes directly from real GIS data. Prefab = uses assigned prefab models.")]
        public NatureMode NatureGenerationMode = NatureMode.ProceduralMesh;

        [Header("Building Models (Prefab Mode)")]
        public GameObject[] BuildingPrefabs;

        [Header("Building Materials")]
        public Material BuildingWallMaterial;
        public Material BuildingRoofMaterial;
        public Material BuildingWindowMaterial;

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

        [Header("Nature Prefabs (Parks & Greenery)")]
        public GameObject[] BushPrefabs;
        public GameObject[] RockPrefabs;
        public GameObject[] GrassPrefabs;

        // Backward compatibility
        public Material BuildingMaterial => BuildingWallMaterial;

        private void Awake()
        {
            ApplyPlayModeOptimizations();
        }

        private void Start()
        {
            ApplyPlayModeOptimizations();
        }

        /// <summary>
        /// Applies runtime distance culling when entering Play Mode.
        /// (Camera.layerCullDistances is a runtime-only property and must be set via script in Play Mode).
        /// </summary>
        public static void ApplyPlayModeOptimizations()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Reset layer cull distances.
                // NOTE: CityCombiner merges grass and props into large spatial chunks (e.g. 350m cells).
                // Camera.layerCullDistances tests against the chunk's bounding sphere center.
                // A short cull distance (such as 110m or 180m) causes Unity to cull the entire 350m chunk
                // whenever the chunk's bounding center is >110m away, even if grass blades are 1m in front of the camera!
                // Setting cull distance to 0 (default in new float[32]) allows standard frustum culling.
                mainCam.layerCullDistances = new float[32];
                mainCam.layerCullSpherical = false;
            }
        }

        [ContextMenu("Restore Camera Cull Distances")]
        public void RestoreCameraCullDistances()
        {
            ApplyPlayModeOptimizations();
            Debug.Log("CityController: Camera.layerCullDistances reset. Grass is now visible in Game View.");
        }
    }
}

