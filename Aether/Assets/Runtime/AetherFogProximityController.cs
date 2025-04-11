using UnityEngine;

namespace Aether
{

    [RequireComponent(typeof(AetherFog))]
    public class AetherFogProximityController : MonoBehaviour
    {

        // Current fog parameters applied to this GameObject.
        public Color CurrentFogColor;
        public float CurrentAetherFogDensity;
        public float CurrentScatterCoefficient;

        // Default values from the AetherFog component.
        public Color DefaultFogColor;
        public float DefaultAetherFogDensity;
        public float DefaultScatterCoefficient;

        // Unity fog density (used for RenderSettings.fogDensity).
        public float CurrentUnityFogDensity;
        public float DefaultUnityFogDensity;

        // Reference to the AetherFog component on this GameObject (can be Global or Sample).
        private AetherFog fogComponent;

        void Awake()
        {
            fogComponent = GetComponent<AetherFog>();
            if (fogComponent == null || (fogComponent.Type != AetherFog.FogType.Global))
            {
                Debug.LogError("GlobalFogManager requires an AetherFog component of type Global or Sample on the same GameObject!");
                return;
            }

            // Record default values.
            DefaultFogColor = fogComponent.Color;
            DefaultAetherFogDensity = fogComponent.Density;
            DefaultScatterCoefficient = fogComponent.ScatterCoefficient;
            DefaultUnityFogDensity = RenderSettings.fogDensity;

            // Initialize current parameters with the defaults.
            CurrentFogColor = DefaultFogColor;
            CurrentAetherFogDensity = DefaultAetherFogDensity;
            CurrentScatterCoefficient = DefaultScatterCoefficient;
            CurrentUnityFogDensity = DefaultUnityFogDensity;
        }

        void Update()
        {
            var targ = AetherFogProximityTarget.Active;
            if (!targ) return;

            Vector3 playerPos = targ.transform.position;
            float cumulativeBlend = 0f;
            Color blendedColor = Color.clear;
            float blendedAetherDensity = 0f;
            float blendedScatter = 0f;
            float blendedUnityDensity = 0f;

            foreach (var fog in AetherFogProximityNode.Pool)
            {
                float blend = fog.GetBlendFactor(playerPos);
                if (blend > 0f)
                {
                    // Compute facing factor based on the dot product between the fog's forward and player's forward.
                    float dot = Vector3.Dot(fog.transform.forward, targ.transform.forward);
                    float facingFactor = (dot + 1f) / 2f; // remap from [-1, 1] to [0, 1]
                    Color effectiveColor = Color.Lerp(fog.SecondaryColor, fog.Color, facingFactor);

                    blendedColor += effectiveColor * blend;
                    blendedAetherDensity += fog.Density * blend;
                    blendedScatter += fog.ScatterCoefficient * blend;
                    blendedUnityDensity += fog.UnityDensity * blend;
                    cumulativeBlend += blend;
                }
            }

            Color targetColor;
            float targetAetherDensity;
            float targetScatter;
            float targetUnityFogDensity;

            if (cumulativeBlend > 0f)
            {
                targetColor = blendedColor / cumulativeBlend;      // Averaged color.
                targetAetherDensity = blendedAetherDensity;          // Keep the reduced density.
                targetScatter = blendedScatter / cumulativeBlend;    // Averaged scatter.
                targetUnityFogDensity = blendedUnityDensity / cumulativeBlend; // Averaged Unity fog density.
            }
            else
            {
                // Revert to defaults.
                targetColor = this.DefaultFogColor;
                targetAetherDensity = this.DefaultAetherFogDensity;
                targetScatter = this.DefaultScatterCoefficient;
                targetUnityFogDensity = this.DefaultUnityFogDensity;
            }

            // Smoothly blend the current global fog settings toward the targets
            this.FadeToFog(targetColor, targetAetherDensity, targetScatter, targ.fadeSpeed * Time.deltaTime, targetUnityFogDensity);
        }

        /// <summary>
        /// Smoothly blends the current fog parameters toward the target values.
        /// The targets passed in should already reflect any reduction from sample fog volumes.
        /// </summary>
        /// <param name="targetColor">Target fog color.</param>
        /// <param name="targetAetherDensity">
        /// Target Aether fog density. For Sample fog, ensure this value is already reduced (i.e. not averaged to cancel out effectScalar).
        /// </param>
        /// <param name="targetScatter">Target scatter coefficient.</param>
        /// <param name="t">Blend factor (typically between 0 and 1).</param>
        /// <param name="targetUnityFogDensity">Target Unity fog density.</param>
        void FadeToFog(Color targetColor, float targetAetherDensity, float targetScatter, float t, float targetUnityFogDensity)
        {
            CurrentFogColor = Color.Lerp(CurrentFogColor, targetColor, t);
            CurrentAetherFogDensity = Mathf.Lerp(CurrentAetherFogDensity, targetAetherDensity, t);
            CurrentScatterCoefficient = Mathf.Lerp(CurrentScatterCoefficient, targetScatter, t);
            CurrentUnityFogDensity = Mathf.Lerp(CurrentUnityFogDensity, targetUnityFogDensity, t);

            // Update the AetherFog component and Unity's fog settings.
            fogComponent.Color = CurrentFogColor;
            fogComponent.Density = CurrentAetherFogDensity;
            fogComponent.ScatterCoefficient = CurrentScatterCoefficient;
            RenderSettings.fogDensity = CurrentUnityFogDensity;
        }

    }

}
