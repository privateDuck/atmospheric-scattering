using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

namespace StormAtmosphere
{
    public sealed class StarPass : ScriptableRenderPass, IDisposable
    {
        private StarFieldObject starField;
        public StarPass(StarFieldObject starField, ref RenderTexture skyTex)
        {
            this.starField = starField;
            this.renderPassEvent = starField.renderPassEvent;
        }
        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmb = CommandBufferPool.Get("cmb_ATM_STARS");
            starField.UpdateStarField(ref cmb);
            context.ExecuteCommandBuffer(cmb);
            CommandBufferPool.Release(cmb);
        }

        public void Dispose()
        {
        }

    }

    public sealed class FogPass : ScriptableRenderPass
    {
        public AtmosphereObject atmosphereObject;
        private Material atmosphericFogMaterial;
        int temporaryRTId = Shader.PropertyToID("_TempRT");
        int fogStrengthID = Shader.PropertyToID("fogStrength");
#if UNITY_EDITOR
        private ProfilingSampler m_fogPass_sampler = new ProfilingSampler("Atm_FogPass");
#endif
        RenderTargetIdentifier source;
        RenderTargetIdentifier destination;

        public FogPass(AtmosphereObject atmosphereObject)
        {
            this.atmosphereObject = atmosphereObject;
            atmosphericFogMaterial ??= new Material(Shader.Find("Hidden/Atmosphere/Fog"));

            atmosphereObject.InitMaterial(atmosphericFogMaterial);

            atmosphericFogMaterial.SetTexture("aerialLuminance", atmosphereObject.aerialLuminance);
            atmosphericFogMaterial.SetTexture("aerialTransmittance", atmosphereObject.aerialTransmittance);

            if (atmosphereObject.skyType == AtmosphereObject.SkyType.SkyBox)
            {
                this.renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
            }
            else
            {
                this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor targetDesc = renderingData.cameraData.cameraTargetDescriptor;
            targetDesc.depthBufferBits = 0;

            source = renderingData.cameraData.renderer.cameraColorTarget;
            cmd.GetTemporaryRT(temporaryRTId, targetDesc, FilterMode.Bilinear);
            destination = new RenderTargetIdentifier(temporaryRTId);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("Atm_Fog_Pass");

            atmosphericFogMaterial.SetFloat(fogStrengthID, atmosphereObject.fogStrength);

#if UNITY_EDITOR
            using (new ProfilingScope(cmd, m_fogPass_sampler))
            {
                if (Application.isPlaying)
                {
                    //Apply fog here
                    Blit(cmd, source, destination, atmosphericFogMaterial, -1);
                    Blit(cmd, destination, source);
                    context.ExecuteCommandBuffer(cmd);
                    UnityEngine.Profiling.Profiler.EndSample();
                }
            }
#else

            Blit(cmd, source, destination, atmosphericFogMaterial, -1);
            Blit(cmd, destination, source);
            context.ExecuteCommandBuffer(cmd);
#endif
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(temporaryRTId);
        }
    }
}