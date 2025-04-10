using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aether.Passes
{

    [System.Serializable]
    public class AetherFogPass : ScriptableRenderPass
    {

        #region Static Interface

        enum State
        {
            Inactive = 0,
            Refresh = 1,
            Active = 2,
        }

        private static State state = State.Active; // defaults active

        public static void StartFog()
        {
            state = State.Refresh;
            Debug.Log("AetherFogPass: Fog started.");
        }

        public static void StopFog()
        {
            state = State.Inactive;
            Debug.Log("AetherFogPass: Fog stopped.");
        }

        #endregion


        const string FOG_SHADER_NAME = "ComputeFog";
        const string RAYMARCH_SHADER_NAME = "RaymarchFog";
        const string BLIT_SHADER_NAME = "Aether/FogApply";
        const string BLUE_NOISE_NAME = "BlueNoise";

        public AetherFogPass(AetherFogPassSettings settings)
        {
            state = State.Active;
            Settings = settings;
            FogCompute = (ComputeShader)Resources.Load(FOG_SHADER_NAME);
            RaymarchCompute = (ComputeShader)Resources.Load(RAYMARCH_SHADER_NAME);
            blueNoise = (Texture)Resources.Load(BLUE_NOISE_NAME);
            // Removed automatic scene load callback to avoid premature refresh.
        }

        public AetherFogPassSettings Settings { get; }
        public ComputeShader FogCompute { get; }
        public ComputeShader RaymarchCompute { get; }

        public RTHandle Target { get; set; }

        [SerializeField] RenderTexture previousFogTexture, fogTexture;

        // Camera
        Camera camera;
        CameraData[] cameraData = new CameraData[1];
        ComputeBuffer cameraDataBuffer;

        // Lights
        AetherLight[] lights;
        LightData[] lightData;
        ComputeBuffer lightDataBuffer;
        int lightCount = 0;

        // Fog Volumes
        AetherFog[] fogVolumes;
        FogData[] fogData;
        ComputeBuffer fogDataBuffer;
        int fogVolumeCount = 0;

        Material blitMaterial;

        Texture blueNoise;

        public static int3 GetDispatchSize(ComputeShader shader, int kernel, int3 desiredThreads)
        {
            uint3 threadGroups;
            shader.GetKernelThreadGroupSizes(kernel, out threadGroups.x, out threadGroups.y, out threadGroups.z);
            return (int3)math.ceil((float3)desiredThreads / (float3)threadGroups);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(Target);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            switch (state)
            {
                case State.Inactive:
                    return;
                case State.Refresh:
                    this.Refresh();
                    break;
            }

            if (!UpdateTextures()) return;
            if (!UpdateCamera()) return;
            if (!UpdateLights()) return;
            if (!UpdateFogVolumes()) return;
            if (!UpdateFogCompute(context)) return;
            if (!UpdateRaymarchCompute(context)) return;
            if (!UpdateMaterial()) return;

            Blit(context);
        }

        public void Blit(ScriptableRenderContext context)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("Aether Blit")))
            {
                Blitter.BlitCameraTexture(cmd, Target, Target, blitMaterial, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            DisposeTextures();
            DisposeCamera();
            DisposeLights();
            DisposeFogVolumes();

            Debug.Log("AetherFogPass: Disposing custom resources.");
        }

        // Refresh method reinitializes internal buffers.
        public void Refresh()
        {
            DisposeTextures();
            DisposeCamera();
            DisposeLights();
            DisposeFogVolumes();

            SetupCamera();
            SetupLights();
            SetupFogVolumes();

            Debug.Log("AetherFogPass: Refreshed internal buffers.");
        }

        //* TEXTURES
        public bool UpdateTextures()
        {
            if (fogTexture == null || previousFogTexture == null) SetupTextures();
            return true;
        }
        public void SetupTextures()
        {
            RenderTextureDescriptor desc = new RenderTextureDescriptor(Settings.VolumeResolution.x, Settings.VolumeResolution.y, RenderTextureFormat.ARGBHalf)
            {
                volumeDepth = Settings.VolumeResolution.z,
                dimension = TextureDimension.Tex3D,
                enableRandomWrite = true,
            };

            DisposeTextures();

            previousFogTexture = new RenderTexture(desc);
            previousFogTexture.Create();
            previousFogTexture.name = "Aether_previousFogTexture";

            fogTexture = new RenderTexture(desc);
            fogTexture.Create();
            fogTexture.name = "Aether_fogTexture";
        }
        public void DisposeTextures()
        {
            if (previousFogTexture != null)
            {
                previousFogTexture.Release();
                Object.DestroyImmediate(previousFogTexture);
                previousFogTexture = null;
            }
            if (fogTexture != null)
            {
                fogTexture.Release();
                Object.DestroyImmediate(fogTexture);
                fogTexture = null;
            }
        }

        //* Camera
        public bool UpdateCamera()
        {
            if (cameraDataBuffer == null) SetupCamera();

            // Use the camera registered by FogCameraReceiver if available.
            camera = AetherFogCameraReceiver.RegisteredCamera ?? Camera.current ?? Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("AetherFogPass: No valid camera found.");
                return false;
            }
            cameraData[0].Update(camera, Settings.ViewDistance);
            cameraDataBuffer.SetData(cameraData);
            return true;
        }
        public void SetupCamera()
        {
            DisposeCamera();
            cameraDataBuffer = new ComputeBuffer(cameraData.Length, CameraData.SIZE);
        }
        public void DisposeCamera()
        {
            if (cameraDataBuffer != null)
            {
                cameraDataBuffer.Release();
                cameraDataBuffer = null;
            }
        }

        //* Lights
        public bool UpdateLights()
        {
            if (lightDataBuffer == null || lights == null) SetupLights();
            if (lightDataBuffer == null) return false;

            for (int i = 0; i < lightCount; i++)
            {
                if (lights[i] != null)
                    lightData[i].Update(lights[i]);
                else
                    lightData[i] = default;
            }
            lightDataBuffer.SetData(lightData);
            return true;
        }
        public void SetupLights()
        {
            lights = Object.FindObjectsOfType<AetherLight>();
            lightCount = lights.Length;
            lightData = new LightData[Mathf.Max(lightCount, 1)];

            DisposeLights();
            lightDataBuffer = new ComputeBuffer(Mathf.Max(lightCount, 1), LightData.SIZE);
        }
        public void DisposeLights()
        {
            if (lightDataBuffer != null)
            {
                lightDataBuffer.Release();
                lightDataBuffer = null;
            }
        }

        //* Fog Volumes
        public bool UpdateFogVolumes()
        {
            if (fogDataBuffer == null || fogVolumes == null) SetupFogVolumes();
            if (fogDataBuffer == null) return false;

            for (int i = 0; i < fogVolumeCount; i++)
            {
                if (fogVolumes[i] != null)
                    fogData[i].Update(fogVolumes[i]);
                else
                    fogData[i] = default;
            }
            fogDataBuffer.SetData(fogData);
            return true;
        }
        public void SetupFogVolumes()
        {
            fogVolumes = Object.FindObjectsOfType<AetherFog>();
            fogVolumeCount = fogVolumes.Length;
            fogData = new FogData[Mathf.Max(fogVolumeCount, 1)];

            DisposeFogVolumes();
            fogDataBuffer = new ComputeBuffer(Mathf.Max(fogVolumeCount, 1), FogData.SIZE);
        }
        public void DisposeFogVolumes()
        {
            if (fogDataBuffer != null)
            {
                fogDataBuffer.Release();
                fogDataBuffer = null;
            }
        }

        //* Fog Compute
        public bool UpdateFogCompute(ScriptableRenderContext context)
        {
            int kernel = FogCompute.FindKernel("ComputeFog");
            FogCompute.SetBool("useMainShadowTexture", AetherShadowPass.UseMainShadowTexture);
            int3 dispatchSize = GetDispatchSize(FogCompute, kernel, Settings.VolumeResolution);
            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            using (new ProfilingScope(cmd, new ProfilingSampler("Aether Fog Compute")))
            {
                cmd.SetComputeTextureParam(FogCompute, kernel, "previousFogTexture", previousFogTexture);
                cmd.SetComputeTextureParam(FogCompute, kernel, "fogTexture", fogTexture);
                cmd.SetComputeVectorParam(FogCompute, "fogTextureResolution", new Vector3(Settings.VolumeResolution.x, Settings.VolumeResolution.y, Settings.VolumeResolution.z));
                cmd.SetComputeBufferParam(FogCompute, kernel, "lightData", lightDataBuffer);
                cmd.SetComputeIntParam(FogCompute, "lightDataLength", lightCount);
                cmd.SetComputeBufferParam(FogCompute, kernel, "fogData", fogDataBuffer);
                cmd.SetComputeIntParam(FogCompute, "fogDataLength", fogVolumeCount);
                cmd.SetComputeBufferParam(FogCompute, kernel, "cameraData", cameraDataBuffer);
                cmd.SetComputeFloatParam(FogCompute, "time", Time.unscaledTime);
                cmd.SetComputeFloatParam(FogCompute, "jitterDistance", Settings.JitterDistance);
                cmd.SetComputeFloatParam(FogCompute, "jitterScale", Settings.JitterScale);
                cmd.SetComputeFloatParam(FogCompute, "temporalStrength", Settings.TemporalStrength);
                cmd.SetComputeTextureParam(FogCompute, kernel, "mainShadowTexture",
                    AetherShadowPass.MainShadowTexture != null ? (Texture)AetherShadowPass.MainShadowTexture : Texture2D.whiteTexture);
                cmd.SetComputeTextureParam(FogCompute, kernel, "blueNoise", blueNoise);
                cmd.SetComputeFloatParam(FogCompute, "blueNoiseSize", blueNoise.width);
                cmd.DispatchCompute(FogCompute, kernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);
            }
            context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Background);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            return true;
        }

        //* Raymarch Compute
        public bool UpdateRaymarchCompute(ScriptableRenderContext context)
        {
            int kernel = RaymarchCompute.FindKernel("RaymarchFog");
            int3 dispatchSize = GetDispatchSize(RaymarchCompute, kernel, Settings.VolumeResolution);
            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            using (new ProfilingScope(cmd, new ProfilingSampler("Aether Raymarch Compute")))
            {
                cmd.SetComputeTextureParam(RaymarchCompute, kernel, "raymarchTexture", fogTexture);
                cmd.SetComputeIntParam(RaymarchCompute, "depthResolution", Settings.VolumeResolution.z);
                cmd.DispatchCompute(RaymarchCompute, kernel, dispatchSize.x, dispatchSize.y, 1);
            }
            context.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Background);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
            return true;
        }

        //* Update Material
        public bool UpdateMaterial()
        {
            if (blitMaterial == null) SetupMaterial();
            blitMaterial.SetTexture("_Volume", fogTexture);
            blitMaterial.SetFloat("_fogFar", Settings.ViewDistance);
            blitMaterial.SetFloat("_cameraFar", camera.farClipPlane);
            return true;
        }
        public void SetupMaterial()
        {
            blitMaterial = new Material(Shader.Find(BLIT_SHADER_NAME));
        }
    }

    [System.Serializable]
    public class AetherFogPassSettings
    {
        public int3 VolumeResolution = new int3(160, 90, 128);
        public float ViewDistance = 70;
        public float JitterDistance = 2;
        public float JitterScale = 3.1f;
        [Range(0, 1)] public float TemporalStrength = .75f;
    }

}
