using UnityEditor;
using UnityEngine;

namespace GeoCity3D.Editor
{
    public static class MaterialFixer
    {
        [MenuItem("GeoCity3D/Fix SimplePoly Materials (Convert to URP)")]
        public static void FixSimplePolyMaterials()
        {
            string[] folders = new[] { "Assets/SimplePoly City - Low Poly Assets" };

            if (!AssetDatabase.IsValidFolder(folders[0]))
            {
                Debug.LogError("SimplePoly City folder not found!");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found! Make sure URP is installed.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", folders);
            int fixedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                // Skip if already using URP
                if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
                    continue;

                // Save the original color before changing shader
                Color originalColor = mat.HasProperty("_Color") ? mat.color : Color.white;
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.mainTexture : null;

                // Switch to URP Lit
                mat.shader = urpLit;

                // Restore color and texture
                mat.color = originalColor;
                if (mainTex != null)
                    mat.mainTexture = mainTex;

                // Set reasonable PBR defaults for low-poly style
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);

                EditorUtility.SetDirty(mat);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Fixed {fixedCount} materials — converted from Legacy to URP Lit shader.");
        }
    }
}
