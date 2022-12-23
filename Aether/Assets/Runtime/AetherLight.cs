using UnityEngine;

namespace Aether
{
    [RequireComponent(typeof(Light))]
    public class AetherLight : MonoBehaviour
    {
        private Light light;

        public Light Light
        {
            get
            {
                if(light == null) light = GetComponent<Light>();
                return light;
            }
        }
    }
}