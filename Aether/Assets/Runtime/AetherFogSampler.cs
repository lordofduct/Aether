using UnityEngine;

namespace Aether
{
    public sealed class AetherFogSampler : MonoBehaviour
    {

        public float fadeSpeed = 1f;

        void Update()
        {
            Vector3 playerPos = transform.position;
            float cumulativeBlend = 0f;
            Color blendedColor = Color.clear;
            float blendedAetherDensity = 0f;
            float blendedScatter = 0f;
            float blendedUnityDensity = 0f;

            foreach (var fog in AetherFog.Pool)
            {
                if (fog.Type != AetherFog.FogType.Sample)
                    continue;

                float blend = fog.GetBlendFactor(playerPos);
                if (blend > 0f)
                {
                    // Compute facing factor based on the dot product between the fog's forward and player's forward.
                    float dot = Vector3.Dot(fog.transform.forward, transform.forward);
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
				targetColor = GlobalFogManager.Instance.DefaultFogColor;
				targetAetherDensity = GlobalFogManager.Instance.DefaultAetherFogDensity;
				targetScatter = GlobalFogManager.Instance.DefaultScatterCoefficient;
				targetUnityFogDensity = GlobalFogManager.Instance.DefaultUnityFogDensity;
			}

            // Smoothly blend the current global fog settings toward the targets
            GlobalFogManager.Instance.FadeToFog(targetColor, targetAetherDensity, targetScatter, fadeSpeed * Time.deltaTime, targetUnityFogDensity);
        }
    }
}
