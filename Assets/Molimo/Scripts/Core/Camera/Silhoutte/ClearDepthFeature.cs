using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace MolimoByLulucaGameDev
{

    public class ClearDepthFeature : ScriptableRendererFeature
    {
        private ClearDepthPass clearDepthPass;

        public override void Create()
        {
            clearDepthPass = new ClearDepthPass(
                RenderPassEvent.BeforeRenderingOpaques
            );
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.name != "Camera_Silhouette")
                return;

            renderer.EnqueuePass(clearDepthPass);
        }
    }

    public class ClearDepthPass : ScriptableRenderPass
    {
        public ClearDepthPass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                "ClearDepth", out var passData))
            {
                passData.DepthTexture = resourceData.activeDepthTexture;

                builder.UseTexture(
                    resourceData.activeDepthTexture,
                    AccessFlags.Write
                );

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers
                        .GetNativeCommandBuffer(ctx.cmd);

                    cmd.ClearRenderTarget(
                        clearDepth: true,
                        clearColor: true,
                        backgroundColor: Color.clear
                    );
                });
            }
        }

        private class PassData
        {
            public TextureHandle DepthTexture;
        }
    }
}
