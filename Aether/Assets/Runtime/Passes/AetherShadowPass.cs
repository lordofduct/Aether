using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace Aether.Passes
{

    [System.Serializable]
    public class AetherShadowPass : ScriptableRenderPass
    {

        public static bool UseMainShadowTexture { get; private set; }
        public static RenderTexture MainShadowTexture { get; private set; }

        public RTHandle MainShadowTarget { get; set; }

        public void CreateTexture()
        {
            // Dispose only if we created this texture (check by name)
            if (MainShadowTexture != null)
            {
                if (MainShadowTexture.name == "Aether_MainShadowTexture")
                {
                    MainShadowTexture.Release();
                    Object.DestroyImmediate(MainShadowTexture);
                    MainShadowTexture = null;
                }
            }
            var mainDesc = MainShadowTarget.rt.descriptor;
            MainShadowTexture = new RenderTexture(mainDesc.width, mainDesc.height, 0, RenderTextureFormat.R16);
            MainShadowTexture.name = "Aether_MainShadowTexture";
            MainShadowTexture.Create();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderer == null)
                return;
                
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            UniversalRenderer universalRenderer = renderingData.cameraData.renderer as UniversalRenderer;
            if(universalRenderer == null) 
            {
                Debug.LogWarning("AetherShadowPass: Unable to cast renderer to UniversalRenderer.");
                return;
            }
            var mainLightPassField = typeof(UniversalRenderer).GetField("m_MainLightShadowCasterPass", flags);
            if(mainLightPassField == null)
            {
                Debug.LogWarning("AetherShadowPass: m_MainLightShadowCasterPass field not found.");
                return;
            }
            MainLightShadowCasterPass mainLightPass = mainLightPassField.GetValue(universalRenderer) as MainLightShadowCasterPass;
            if(mainLightPass == null)
            {
                Debug.LogWarning("AetherShadowPass: MainLightShadowCasterPass is null.");
                return;
            }
            var mainShadowTargetField = typeof(MainLightShadowCasterPass).GetField("m_MainLightShadowmapTexture", flags);
            if(mainShadowTargetField == null)
            {
                Debug.LogWarning("AetherShadowPass: m_MainLightShadowmapTexture field not found.");
                return;
            }
            MainShadowTarget = mainShadowTargetField.GetValue(mainLightPass) as RTHandle;
            if(MainShadowTarget == null || MainShadowTarget.rt == null)
            {
                //TODO - currently this is being stupid, Andy and I need to swoop back around to these scripts and make this all work - dylane
                //Debug.LogWarning("AetherShadowPass: MainShadowTarget or its texture is null.");
                return;
            }

            // Configure target but do not override URP's internal resources.
            ConfigureTarget(MainShadowTarget);
        }

        public bool CompareRT(Texture a, Texture b)
        {
            return a != null && b != null && a.height == b.height && a.width == b.width;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            UseMainShadowTexture = renderingData.shadowData.supportsMainLightShadows;
            if (!UseMainShadowTexture) return;
            if(MainShadowTarget == null || MainShadowTarget.rt == null)
                return;
                
            if (!CompareRT(MainShadowTarget.rt, MainShadowTexture)) CreateTexture();

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Aether Shadow Pass")))
            {
                cmd.CopyTexture(MainShadowTarget.rt, MainShadowTexture);
                cmd.SetGlobalTexture("_MainShadowTexture", MainShadowTexture);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

    }

}
