using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aether
{

    public sealed class AetherFog : MonoBehaviour
    {

        public enum FogType
        {
            Local,
            Global
        }

        #region Fields

        public FogType Type;
        public Color Color;
        [Range(0, 1)] public float Density;
        [Range(0, 1)] public float ScatterCoefficient = 0.9f;

        #endregion

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            if (Type == FogType.Global)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
#endif

    }

}
