using UnityEngine;

namespace GeoCity3D.Visuals
{
    /// <summary>
    /// Animates water surface texture and ripple normal map UV offsets in real-time.
    /// Operates in both Unity Editor Scene View ([ExecuteAlways]) and Play Mode.
    /// Simulates downstream currents for rivers and dual-wave ripple drift for lakes.
    /// </summary>
    [ExecuteAlways]
    public class WaterAnimator : MonoBehaviour
    {
        [Header("Flow & Wave Settings (Disabled for static calm water)")]
        [Tooltip("Speed of downstream water flow (0 = static calm water)")]
        public float FlowSpeed = 0f;

        [Tooltip("Direction of river current in UV space")]
        public Vector2 FlowDirection = new Vector2(0f, 1f);

        [Tooltip("Speed of secondary surface ripple wavelets (0 = static calm water)")]
        public float RippleSpeed = 0f;

        [Tooltip("Secondary wave drift direction for dual-wave interference")]
        public Vector2 RippleDirection = new Vector2(0.6f, 0.8f);

        [Tooltip("Whether this water body is a river (directional flow) or lake (gentle orbital drift)")]
        public bool IsRiver = true;

        [Header("Material Reference (Optional - uses attached renderer if null)")]
        public Material TargetMaterial;

        private Renderer _renderer;
        private Material _activeMat;

        private void OnEnable()
        {
            Initialize();
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            if (TargetMaterial != null)
            {
                _activeMat = TargetMaterial;
            }
            else if (_renderer != null)
            {
                _activeMat = _renderer.sharedMaterial;
            }
        }

        private void Update()
        {
            if (FlowSpeed <= 0.0001f && RippleSpeed <= 0.0001f) return;

            if (_activeMat == null)
            {
                Initialize();
                if (_activeMat == null) return;
            }

            float time = 0f;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                time = (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
            else
            {
                time = Time.time;
            }
#else
            time = Time.time;
#endif

            Vector2 mainOffset;
            Vector2 bumpOffset;

            if (IsRiver)
            {
                // River: Flow continuously along FlowDirection (along length), with subtle lateral sway
                float lateralSway = Mathf.Sin(time * 1.5f) * 0.03f;
                float flowDist = time * FlowSpeed;
                mainOffset = new Vector2(lateralSway, flowDist);

                // Normal map ripples move slightly faster with a small angle offset to simulate surface turbulence
                float rippleDist = time * (FlowSpeed * 1.35f);
                bumpOffset = new Vector2(lateralSway * 1.4f + Mathf.Cos(time * 2.2f) * 0.02f, rippleDist);
            }
            else
            {
                // Lake: Multi-directional gentle wave drift & swell oscillation
                float driftX = Mathf.Sin(time * RippleSpeed * 0.7f) * 0.15f + time * (RippleDirection.x * RippleSpeed * 0.5f);
                float driftY = Mathf.Cos(time * RippleSpeed * 0.9f) * 0.15f + time * (RippleDirection.y * RippleSpeed * 0.5f);
                mainOffset = new Vector2(driftX, driftY);

                float bumpX = Mathf.Cos(time * RippleSpeed * 1.1f) * 0.20f + time * (RippleDirection.x * RippleSpeed * 0.8f);
                float bumpY = Mathf.Sin(time * RippleSpeed * 1.3f) * 0.20f - time * (RippleDirection.y * RippleSpeed * 0.8f);
                bumpOffset = new Vector2(bumpX, bumpY);
            }

            // Apply to active material
            if (_activeMat.HasProperty("_MainTex"))
            {
                _activeMat.SetTextureOffset("_MainTex", mainOffset);
            }

            if (_activeMat.HasProperty("_BumpMap"))
            {
                _activeMat.SetTextureOffset("_BumpMap", bumpOffset);
            }
        }
    }
}
