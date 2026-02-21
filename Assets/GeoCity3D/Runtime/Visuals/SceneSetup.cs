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
            SetupSunLight(cityRadius);
            SetupAmbient();
            TrySetupPostProcessing();
        }

        // ═══════════════════════════════════════════════════════════
        //  SUN LIGHT — directional light with shadows
        // ═══════════════════════════════════════════════════════════

        private static void SetupSunLight(float cityRadius)
        {
            // Reuse existing directional light or create one
            Light sun = null;
            Light[] allLights = Object.FindObjectsOfType<Light>();
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    sun = l;
                    break;
                }
            }

            if (sun == null)
            {
                GameObject sunGo = new GameObject("Sun");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                Debug.Log("SceneSetup: Created directional sun light.");
            }

            // Warm afternoon sun angle — 50° from horizon for long shadows
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Warm afternoon color
            sun.color = new Color(1.0f, 0.96f, 0.88f);
            sun.intensity = 1.2f;

            // Shadows
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;
            sun.shadowNearPlane = 0.1f;

            // Shadow distance for city-scale
            float shadowDist = Mathf.Max(cityRadius * 2f, 500f);

            // ── URP Pipeline Asset configuration ──
            // URP ignores QualitySettings for shadows — must set on the pipeline asset
            var pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset != null)
            {
                var assetType = pipelineAsset.GetType();

                // Enable main light shadows
                var castShadowsProp = assetType.GetProperty("supportsMainLightShadows");
                if (castShadowsProp != null && castShadowsProp.CanWrite)
                    castShadowsProp.SetValue(pipelineAsset, true);

                // Shadow distance
                var shadowDistProp = assetType.GetProperty("shadowDistance");
                if (shadowDistProp != null && shadowDistProp.CanWrite)
                    shadowDistProp.SetValue(pipelineAsset, shadowDist);

                // Shadow cascades (4 for best quality)
                var cascadeProp = assetType.GetProperty("shadowCascadeCount");
                if (cascadeProp != null && cascadeProp.CanWrite)
                    cascadeProp.SetValue(pipelineAsset, 4);

                // Shadow resolution — try to set to highest (4096)
                var resField = assetType.GetProperty("mainLightShadowmapResolution");
                if (resField != null && resField.CanWrite)
                {
                    try { resField.SetValue(pipelineAsset, 4096); }
                    catch { /* resolution enum may differ */ }
                }

                // Additional lights shadows
                var addShadowsProp = assetType.GetProperty("supportsAdditionalLightShadows");
                if (addShadowsProp != null && addShadowsProp.CanWrite)
                    addShadowsProp.SetValue(pipelineAsset, true);

                Debug.Log($"SceneSetup: URP pipeline configured — shadows ON, distance: {shadowDist}m, 4 cascades");

                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(pipelineAsset);
                #endif
            }
            else
            {
                // Built-in pipeline fallback
                QualitySettings.shadowDistance = shadowDist;
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowCascades = 4;
                Debug.Log($"SceneSetup: Built-in pipeline — shadows ON, distance: {shadowDist}m");
            }
        }

        private static void SetupAmbient()
        {
            // Bright ambient so shadows aren't pitch black
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // Warm, bright afternoon light (slightly yellowish/orange tint)
            RenderSettings.ambientLight = new Color(0.85f, 0.82f, 0.75f);
            RenderSettings.ambientIntensity = 1.5f;

            // ── Procedural Skybox ──
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material skyMat = new Material(skyShader);
                skyMat.SetColor("_SkyTint", new Color(0.53f, 0.71f, 1.0f));    // Clean light blue
                skyMat.SetColor("_GroundColor", new Color(0.85f, 0.85f, 0.80f)); // Light warm horizon
                skyMat.SetFloat("_Exposure", 1.1f);
                skyMat.SetFloat("_SunSize", 0.04f);
                skyMat.SetFloat("_SunSizeConvergence", 8f);
                skyMat.SetFloat("_AtmosphereThickness", 0.8f);  // Lower = cleaner blue, less yellow
                RenderSettings.skybox = skyMat;
                Debug.Log("SceneSetup: Procedural skybox applied.");
            }

            // ── Distance Fog ──
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.75f, 0.82f); // Hazy blue-grey
            RenderSettings.fogDensity = 0.0012f; // Subtle — visible at ~500m+
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
                            { "intensity", 0.45f }, // Increased for a brighter, dreamier afternoon look
                            { "threshold", 1.0f },
                            { "scatter", 0.65f }
                        });

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "intensity", 0.2f }, // Slightly reduced so it doesn't darken the edges as much
                            { "smoothness", 0.4f }
                        });

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "contrast", 15f },  // More pop
                            { "saturation", 25f } // Significantly more colorful
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
