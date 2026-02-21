
using UnityEditor;

public class FBXScaleFixer
{
    [MenuItem("GeoCity3D/Fix FBX Scales")]
    public static void FixScales()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Residential Buildings Set" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    importer.globalScale = 1f;
                    importer.SaveAndReimport();
                }

                // Log the actual mesh bounds for debugging
                UnityEngine.GameObject prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
                if (prefab != null)
                {
                    var mr = prefab.GetComponentInChildren<UnityEngine.MeshFilter>();
                    if (mr != null && mr.sharedMesh != null)
                        UnityEngine.Debug.Log($"Model: {path} | Mesh Bounds: {mr.sharedMesh.bounds.size}");
                }
            }
        }
        UnityEngine.Debug.Log("FBX scales reset to 1. Check mesh bounds above.");
    }
}

