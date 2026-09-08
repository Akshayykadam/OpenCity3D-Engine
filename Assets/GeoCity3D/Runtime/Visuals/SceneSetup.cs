using UnityEngine;
using UnityEngine.Rendering;

namespace GeoCity3D.Visuals
{
    /// <summary>
    /// Realistic scene lighting, atmosphere, shadows, reflection probes, and aerial perspective.
    /// Operates across both Built-in Render Pipeline and Universal Render Pipeline (URP).
    /// </summary>
    public static class SceneSetup
    {
        public static void Setup(float cityRadius = 500f)
        {
            SetupSunLight(cityRadius);
            SetupAmbient(cityRadius);
            RemoveReflectionProbe();
            SetupCamera(cityRadius);
            TrySetupPostProcessing();
        }

        // ═══════════════════════════════════════════════════════════
        //  SUN LIGHT — Directional sunlight with calibrated soft shadows
        // ═══════════════════════════════════════════════════════════

        private static void SetupSunLight(float cityRadius)
        {
            Light sun = null;
#if UNITY_2023_1_OR_NEWER
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            Light[] allLights = Object.FindObjectsOfType<Light>();
#endif
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
            }

            // Crisp afternoon sun angle (46° elevation, -38° azimuth)
            // Creates natural building facade shadow contrast along streets without flat overexposure
            sun.transform.rotation = Quaternion.Euler(46f, -38f, 0f);

            // Natural warm daylight (crisp, balanced sunlight)
            sun.color = new Color(1.0f, 0.96f, 0.90f);
            sun.intensity = 1.0f;

            // Soft realistic shadows with tight bias to prevent shadow detachment / Peter-Panning
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.005f;
            sun.shadowNormalBias = 0.35f;
            sun.shadowNearPlane = 0.15f;

            // Calibrated shadow distance: provides crisp soft shadows in view while cutting shadow pass work by ~75%
            float shadowDist = Mathf.Clamp(cityRadius * 0.45f, 180f, 250f);

            var pipelineAsset = GraphicsSettings.currentRenderPipeline;
            if (pipelineAsset != null)
            {
                var assetType = pipelineAsset.GetType();

                var castShadowsProp = assetType.GetProperty("supportsMainLightShadows");
                if (castShadowsProp != null && castShadowsProp.CanWrite)
                    castShadowsProp.SetValue(pipelineAsset, true);

                var shadowDistProp = assetType.GetProperty("shadowDistance");
                if (shadowDistProp != null && shadowDistProp.CanWrite)
                    shadowDistProp.SetValue(pipelineAsset, shadowDist);

                var cascadeProp = assetType.GetProperty("shadowCascadeCount");
                if (cascadeProp != null && cascadeProp.CanWrite)
                    cascadeProp.SetValue(pipelineAsset, 4);

                var resField = assetType.GetProperty("mainLightShadowmapResolution");
                if (resField != null && resField.CanWrite)
                {
                    try { resField.SetValue(pipelineAsset, 4096); }
                    catch { /* Enum type fallback */ }
                }

                var addShadowsProp = assetType.GetProperty("supportsAdditionalLightShadows");
                if (addShadowsProp != null && addShadowsProp.CanWrite)
                    addShadowsProp.SetValue(pipelineAsset, true);

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(pipelineAsset);
#endif
            }
            else
            {
                // Built-in pipeline high-fidelity shadows & antialiasing
                QualitySettings.shadowDistance = shadowDist;
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowCascades = 4;
                QualitySettings.shadowCascade4Split = new Vector3(0.06f, 0.20f, 0.50f);
                QualitySettings.antiAliasing = 4;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AMBIENT LIGHTING & AERIAL FOG
        // ═══════════════════════════════════════════════════════════

        private static void SetupAmbient(float cityRadius)
        {
#if UNITY_EDITOR
            // ── Auto-apply Polyverse Skies Blue Sky if available ──
            if (RenderSettings.skybox == null)
            {
                string[] skyGuids = UnityEditor.AssetDatabase.FindAssets("Polyverse Skies - Blue Sky t:Material");
                if (skyGuids != null && skyGuids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(skyGuids[0]);
                    Material skyMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (skyMat != null) RenderSettings.skybox = skyMat;
                }
            }
#endif

            // Fallback to procedural skybox if no asset present
            if (RenderSettings.skybox == null)
            {
                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    Material skyMat = new Material(skyShader);
                    skyMat.name = "Procedural_DaySkybox";
                    skyMat.SetColor("_SkyTint", new Color(0.53f, 0.71f, 1.0f));       // Clean daylight blue
                    skyMat.SetColor("_GroundColor", new Color(0.85f, 0.85f, 0.80f));  // Warm horizon
                    skyMat.SetFloat("_Exposure", 1.1f);
                    skyMat.SetFloat("_SunSize", 0.04f);
                    skyMat.SetFloat("_SunSizeConvergence", 8f);
                    skyMat.SetFloat("_AtmosphereThickness", 0.8f);
                    RenderSettings.skybox = skyMat;
                }
            }

            // ── Realistic Ambient Lighting (Skybox Spherical Harmonics) ──
            // Ambient light is derived from the actual skybox dome:
            // upward surfaces catch sky blue, sideways surfaces catch horizon haze, shadows have natural soft cool fill!
            if (RenderSettings.skybox != null)
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.10f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.defaultReflectionResolution = 512;
                RenderSettings.reflectionIntensity = 1.0f;
            }
            else
            {
                // Trilight gradient fallback
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.55f, 0.72f, 0.92f);
                RenderSettings.ambientEquatorColor = new Color(0.70f, 0.72f, 0.76f);
                RenderSettings.ambientGroundColor = new Color(0.22f, 0.32f, 0.18f);
                RenderSettings.ambientIntensity = 1.10f;
            }

            DynamicGI.UpdateEnvironment();

            // ── Atmospheric Aerial Perspective Fog ──
            // Linear fog blends distant city horizon seamlessly into sky without fogging foreground
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.78f, 0.90f);
            RenderSettings.fogStartDistance = Mathf.Max(cityRadius * 0.70f, 250f);
            RenderSettings.fogEndDistance = Mathf.Max(cityRadius * 2.4f, 1000f);
        }

        // ═══════════════════════════════════════════════════════════
        //  CLEANUP REFLECTION PROBE (Skybox cubemap handles reflections naturally)
        // ═══════════════════════════════════════════════════════════

        private static void RemoveReflectionProbe()
        {
#if UNITY_2023_1_OR_NEWER
            ReflectionProbe[] allProbes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
#else
            ReflectionProbe[] allProbes = Object.FindObjectsOfType<ReflectionProbe>();
#endif
            foreach (var p in allProbes)
            {
                if (p != null && p.gameObject.name == "CityReflectionProbe")
                {
                    Object.DestroyImmediate(p.gameObject);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  CAMERA ALIGNMENT
        // ═══════════════════════════════════════════════════════════

        private static void SetupCamera(float cityRadius)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.Skybox;
                mainCam.farClipPlane = Mathf.Max(mainCam.farClipPlane, cityRadius * 3.5f);

                // Layer-based spherical distance culling:
                // Unity's C++ camera pipeline skips distant dense micro-geometry automatically
                float[] cullDistances = new float[32];
                int grassLayer = LayerMask.NameToLayer("Grass");
                if (grassLayer >= 0)
                    cullDistances[grassLayer] = 110f; // Individual grass clumps culled beyond 110m

                int propsLayer = LayerMask.NameToLayer("Props");
                if (propsLayer >= 0)
                    cullDistances[propsLayer] = 180f; // Micro props culled beyond 180m

                mainCam.layerCullDistances = cullDistances;
                mainCam.layerCullSpherical = true;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  POST-PROCESSING (URP Volume fallback)
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

                if (volumeType == null) return;

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
                            { "intensity", 0.35f },
                            { "threshold", 1.05f },
                            { "scatter", 0.65f }
                        });

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "intensity", 0.18f },
                            { "smoothness", 0.35f }
                        });

                    TryAddVolumeOverride(profile, "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime",
                        new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "contrast", 12f },
                            { "saturation", 18f }
                        });
                }
            }
            catch { /* Skip silently if URP runtime not present */ }
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
