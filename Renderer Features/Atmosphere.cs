using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FleetCommander.Graphics
{
    public class Atmosphere : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public AtmosphereSettings AtmosphereSettings;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            public Material skyMaterial = null;
            public int materialPassIndex = -1;

        }

        public Settings settings = new Settings();


        AtmospherePass m_Pass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_Pass = new AtmospherePass(settings, name);

            // Configures where the render pass should be injected.
            m_Pass.renderPassEvent = settings.renderPassEvent;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_Pass);
        }

        //Actual RenderPass
        class AtmospherePass : ScriptableRenderPass
        {
            public Settings settings;
            string m_ProfilerTag;
            public FilterMode filterMode { get; set; }
            int temporaryRTId = Shader.PropertyToID("_TempRT");

            RenderTargetIdentifier source;
            RenderTargetIdentifier destination;

            public AtmospherePass(Settings settings, string tag)
            {
                this.settings = settings;
                m_ProfilerTag = tag;

            }

            private void UpdateSkyParams()
            {
                //settings.AtmosphereSettings.
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


                if (Application.isPlaying)
                {
                    if (settings.AtmosphereSettings.requiresInitialization)
                        settings.AtmosphereSettings.OnInitialize();

                    settings.AtmosphereSettings.UpdateAll(ref cmd);
                }
                var renderer = renderingData.cameraData.renderer;
                source = renderingData.cameraData.renderer.cameraColorTarget;
                cmd.GetTemporaryRT(temporaryRTId, targetDesc, filterMode);
                destination = new RenderTargetIdentifier(temporaryRTId);
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get(m_ProfilerTag);
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Blit(cmd, source, destination, settings.skyMaterial, settings.materialPassIndex);
                    Blit(cmd, destination, source);
                    context.ExecuteCommandBuffer(cmd);
                }
#else

                Blit(cmd, source, destination, settings.overrideMaterial, settings.materialPassIndex);
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
}


