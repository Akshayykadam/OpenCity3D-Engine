using UnityEditor;
using UnityEngine;

public class InspectFBXScale
{
    [MenuItem("GeoCity3D/Inspect FBX Scale")]
    public static void InspectScale()
    {
        string path = "Assets/GeoCity3D/Models/Residential Buildings Set/Residential Buildings 001.fbx";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            var mr = prefab.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                Debug.Log($"Prefab: {prefab.name} Scale: {prefab.transform.localScale} Bounds Size: {mr.bounds.size}");
            }
            else
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Debug.Log($"Prefab: {prefab.name} Scale: {prefab.transform.localScale} Mesh Bounds Size: {mf.sharedMesh.bounds.size}");
                }
            }
        }
    }
}
