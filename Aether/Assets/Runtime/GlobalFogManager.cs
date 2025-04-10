using UnityEngine;

namespace Aether
{
    public class GlobalFogManager : MonoBehaviour
    {
        public static GlobalFogManager Instance;

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
            Instance = this;
            fogComponent = GetComponent<AetherFog>();

            if (fogComponent == null || (fogComponent.Type != AetherFog.FogType.Global && fogComponent.Type != AetherFog.FogType.Sample))
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
        public void FadeToFog(Color targetColor, float targetAetherDensity, float targetScatter, float t, float targetUnityFogDensity)
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
