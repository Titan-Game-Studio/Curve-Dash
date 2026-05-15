using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace STG.CurveDash.RenderFeatures
{
    public class PickupOutlineFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OutlineSettings
        {
            [Tooltip("Layer of the objects to outline. Pickups should be set to Pickup.")]
            public LayerMask OutlineLayer = 1 << 0; 
            public Color OutlineColor = new Color(1f, 0.8f, 0f, 1f);
            [Range(1f, 10f)] public float OutlineWidth = 2f;
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public OutlineSettings settings = new OutlineSettings();
        private PickupOutlinePass m_Pass;

        public override void Create()
        {
            m_Pass = new PickupOutlinePass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
        }

        class PickupOutlinePass : ScriptableRenderPass
        {
            private OutlineSettings settings;
            private Material material;
            private ShaderTagId shaderTagId = new ShaderTagId("UniversalForward");

            public PickupOutlinePass(OutlineSettings settings)
            {
                this.settings = settings;
                this.renderPassEvent = settings.passEvent;
                Shader shader = Shader.Find("Hidden/CurveDash/PickupOutline");
                if (shader != null)
                {
                    material = CoreUtils.CreateEngineMaterial(shader);
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(material);
            }

            private class MaskPassData
            {
                public RendererListHandle rendererList;
            }

            private class CompositePassData
            {
                public Material material;
                public TextureHandle sourceMask;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                // 1. Create Mask Texture Descriptor
                TextureDesc textureDesc = new TextureDesc(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);
                textureDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                textureDesc.depthBufferBits = 0;
                textureDesc.name = "_SilhouetteMask";
                textureDesc.clearBuffer = true;
                textureDesc.clearColor = Color.black;

                TextureHandle maskTexHandle = renderGraph.CreateTexture(textureDesc);

                // --- PASS 1: RENDER MASK ---
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Outline Mask Pass", out var passData))
                {
                    FilteringSettings filter = new FilteringSettings(RenderQueueRange.all, settings.OutlineLayer);

                    SortingSettings sortingSettings = new SortingSettings(cameraData.camera);
                    sortingSettings.criteria = SortingCriteria.CommonOpaque;
                    DrawingSettings drawSettings = new DrawingSettings(shaderTagId, sortingSettings);
                    drawSettings.overrideMaterial = material;
                    drawSettings.overrideMaterialPassIndex = 0; // SolidMask

                    UniversalRenderingData uniRenderingData = frameData.Get<UniversalRenderingData>();
                    var param = new RendererListParams(uniRenderingData.cullResults, drawSettings, filter);
                    passData.rendererList = renderGraph.CreateRendererList(param);

                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(maskTexHandle, 0, AccessFlags.Write);
                    
                    if (resourceData.activeDepthTexture.IsValid())
                    {
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    }

                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }

                // --- PASS 2: COMPOSITE OUTLINE ---
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Outline Composite Pass", out var passData))
                {
                    passData.material = material;
                    passData.sourceMask = maskTexHandle;

                    builder.UseTexture(maskTexHandle, AccessFlags.Read);

                    // We write to activeColor using Alpha Blending (done in shader)
                    TextureHandle activeColor = resourceData.activeColorTexture;
                    builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);

                    material.SetColor("_OutlineColor", settings.OutlineColor);
                    material.SetFloat("_OutlineWidth", settings.OutlineWidth);

                    builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                    {
                        data.material.SetTexture("_SilhouetteMask", data.sourceMask);
                        // Draw fullscreen triangle
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 1, MeshTopology.Triangles, 3, 1);
                    });
                }
            }

            // Provide dummy overrides for old pipeline so it doesn't throw the warning
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {}
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {}
        }
    }
}
