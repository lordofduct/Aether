using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Aether.AetherFog;

namespace Aether
{

    public class AetherFogProximityNode : MonoBehaviour
    {

        public static readonly HashSet<AetherFogProximityNode> Pool = new();


        #region Fields

        public Color Color;
        [Range(0, 1)] public float Density;
        [Range(0, 1)] public float ScatterCoefficient = 0.9f;

        [Tooltip("Secondary color used when the player's facing direction is opposite to the fog volume's facing.")]
        public Color SecondaryColor = Color.white;

        [Tooltip("Density for Unity's built-in fog provided by this sample fog.")]
        public float UnityDensity = 0.01f;

        // Only used when Type is Sample:
        [Tooltip("Radius within which the fog effect is at 100% strength.")]
        public float fullEffectRadius = 5f;
        [Tooltip("Radius beyond which the fog effect falls off to 0.")]
        public float falloffRadius = 10f;
        [Tooltip("Modifier to scale down the strength of Sample fog (0 to 1).")]
        [Range(0, 1)] public float effectScalar = 1f;

        #endregion

        #region CONSTRUCTOR

        private void OnEnable()
        {
            Pool.Add(this);
        }

        private void OnDisable()
        {
            Pool.Remove(this);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a blend factor (0 to effectScalar) based on distance to a point.
        /// In Global or Local modes it always returns 1.
        /// </summary>
        public float GetBlendFactor(Vector3 point)
        {
            float distance = Vector3.Distance(transform.position, point);
            float blend = 1f;
            if (distance <= fullEffectRadius)
            {
                blend = 1f;
            }
            else if (distance >= falloffRadius)
            {
                blend = 0f;
            }
            else
            {
                // Linear falloff between fullEffectRadius and falloffRadius.
                blend = 1f - ((distance - fullEffectRadius) / (falloffRadius - fullEffectRadius));
            }

            // Multiply by effectScalar so that even if fully inside the zone,
            // the contribution is reduced according to effectScalar.
            return blend * effectScalar;
        }

        #endregion

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, transform.localScale);

            // Visualize the effective radii for the sample fog:
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, fullEffectRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, falloffRadius);
        }
#endif

    }

}
