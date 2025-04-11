using System.Collections;
using UnityEngine;
using Aether.Passes;

namespace Aether
{

    [RequireComponent(typeof(Camera))]
    public class AetherFogCameraReceiver : MonoBehaviour
    {

        public static Camera RegisteredCamera { get; private set; }

        private Camera _camera;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            RegisteredCamera = _camera;
            // Delay the refresh until the end of frame to let URP finish its initialization.
            StartCoroutine(DelayedStart());
        }

        IEnumerator DelayedStart()
        {
            yield return new WaitForEndOfFrame();
            AetherFogPass.StartFog();
            Debug.Log("AetherFogCameraReceiver: Fog refreshed and started with " + RegisteredCamera.name);
        }

        void OnDisable()
        {
            if (RegisteredCamera == _camera)
            {
                AetherFogPass.StopFog();
                Debug.Log("AetherFogCameraReceiver: Fog stopped from " + RegisteredCamera.name);
                RegisteredCamera = null;
            }
        }
    }

}
