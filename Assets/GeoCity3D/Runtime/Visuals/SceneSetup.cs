using UnityEngine;
using UnityEngine.Rendering;

namespace GeoCity3D.Visuals
{
    /// <summary>
    /// Optional post-processing setup. Lighting, fog, and skybox are left to the user.
    /// </summary>
    public static class SceneSetup
    {
        public static void Setup(float cityRadius = 500f)
        {
            SetupAmbient();
            TrySetupPostProcessing();
        }

        private static void SetupAmbient()
        {
            // Bright ambient so shadows aren't pitch black
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.60f); // Bright neutral gray
            RenderSettings.ambientIntensity = 1.2f;

            // Ensure shadows are enabled if in Built-in pipeline
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            // Set shadow distance to see the whole city (default is often only 50m)
            QualitySettings.shadowDistance = 500f; 
        }

        // ═══════════════════════════════════════════════════════════
        //  POST-PROCESSING — bloom, vignette, color grading (URP)
        // ═══════════════════════════════════════════════════════════

        private static void TrySetupPostProcessing()
        {
            try
            {
                System.Type volumeType = System.Type.GetType(
                    "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");

                if (volumeType == null)
                    volumeType = System.Type.GetType(
                        "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

                if (volumeType == null)
                {
                    Debug.Log("SceneSetup: URP Volume not found — skipping post-processing.");
                    return;
                }

                GameObject volumeGo = new GameObject("PostProcessing Volume");
                var volumeComp = volumeGo.AddComponent(volumeType) as Component;
                if (volumeComp == null) return;

                var isGlobalProp = volumeType.GetProperty("isGlobal");
                if (isGlobalProp != null)
                    isGlobalProp.SetValue(volumeComp, true);

                System.Type profileType = System.Type.GetType(
                    "UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime");
                if (profileType != null)
                {
                    var profile = ScriptableObject.CreateInstance(profileType);
                    var profileProp = volumeType.GetProperty("profile") ??
                                     volumeType.GetProperty("sharedProfile");
                    if (profileProp != null)
                        profileProp.SetValue(volumeComp, profile);

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "intensity", 0.3f },
                            { "threshold", 1.1f },
                            { "scatter", 0.65f }
                        });

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "intensity", 0.25f },
                            { "smoothness", 0.4f }
                        });

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "contrast", 10f },
                            { "saturation", 8f }
                        });
                }

                Debug.Log("SceneSetup: Post-processing volume created (Bloom, Vignette, Color Adjustments)");
            }
            catch (System.Exception e)
            {
                Debug.Log($"SceneSetup: Post-processing setup skipped — {e.Message}");
            }
        }

        private static void TryAddVolumeOverride(object profile, string typeName,
            System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                System.Type overrideType = System.Type.GetType(typeName);
                if (overrideType == null) return;

                var addMethod = profile.GetType().GetMethod("Add",
                    new System.Type[] { typeof(System.Type), typeof(bool) });
                if (addMethod == null)
                    addMethod = profile.GetType().GetMethod("Add");
                if (addMethod == null) return;

                object component = null;
                try { component = addMethod.Invoke(profile, new object[] { overrideType, true }); }
                catch
                {
                    try { component = addMethod.Invoke(profile, new object[] { overrideType }); }
                    catch { return; }
                }
                if (component == null) return;

                var activeProp = component.GetType().GetProperty("active");
                if (activeProp != null)
                    activeProp.SetValue(component, true);

                foreach (var kvp in parameters)
                {
                    var field = component.GetType().GetField(kvp.Key);
                    if (field == null) continue;

                    var param = field.GetValue(component);
                    if (param == null) continue;

                    var overrideField = param.GetType().GetField("overrideState");
                    if (overrideField != null)
                        overrideField.SetValue(param, true);

                    var valueProp = param.GetType().GetProperty("value");
                    if (valueProp != null)
                        valueProp.SetValue(param, System.Convert.ChangeType(kvp.Value, valueProp.PropertyType));
                }
            }
            catch { /* Skip silently */ }
        }
    }
}
