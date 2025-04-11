using UnityEngine;

namespace Aether
{
    public sealed class AetherFogProximityTarget : MonoBehaviour
    {

        public static AetherFogProximityTarget Active { get; private set; }

        public float fadeSpeed = 1f;

        #region CONSTRUCTOR

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        #endregion

    }
}
