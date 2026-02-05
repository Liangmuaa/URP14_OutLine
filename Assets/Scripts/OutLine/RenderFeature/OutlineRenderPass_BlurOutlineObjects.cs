// /************************************************************
// FileName: OutlineRenderPass_BlurOutlineObjects.cs
// Author: ZJL
// Date:2026-1-23 15:1
// Description: 模糊描边物体并且剔除出描边
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SVFramework.URP
{
    public class OutlineRenderPass_BlurOutlineObjects : ScriptableRenderPass
    {
        private static readonly int s_OffsetsShaderId = Shader.PropertyToID("_Offsets");
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("OutlineRenderPass_BlurOutlineObjects");
        private Material m_OutlineEffectMaterial;
        private RTHandle m_TempRTHandle1;
        private RTHandle m_TempRTHandle2;

        private RTHandle m_Source;
        private RTHandle m_Output;

        private float m_SamplerArea;
        private int m_Iteration;
        private int m_DownSample;

        public OutlineRenderPass_BlurOutlineObjects(RenderPassEvent evt, Material outlineEffectMaterial)
        {
            renderPassEvent = evt;
            m_OutlineEffectMaterial = outlineEffectMaterial;
        }

        public void Setup(ref RTHandle scr, ref RTHandle dest, float samplerArea, int iteration, int sampleDown)
        {
            m_Source = scr;
            m_Output = dest;
            m_SamplerArea = samplerArea;
            m_Iteration = iteration;
            m_DownSample = sampleDown;
            m_OutlineEffectMaterial.SetTexture(scr.name, scr);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (m_Output == null)
            {
                return;
            }

            var desc = cameraTextureDescriptor;
            desc.width >>= m_DownSample;
            desc.height >>= m_DownSample;

            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
            RenderingUtils.ReAllocateIfNeeded(ref m_TempRTHandle1, desc, FilterMode.Bilinear, TextureWrapMode.Clamp);
            RenderingUtils.ReAllocateIfNeeded(ref m_TempRTHandle2, desc, FilterMode.Bilinear, TextureWrapMode.Clamp);

            ConfigureTarget(m_Output);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;

            if (m_OutlineEffectMaterial == null)
                return;

            if (m_Output == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(m_SamplerArea, 0, 0, 0));
                Blitter.BlitCameraTexture(cmd, m_Source, m_TempRTHandle1, m_OutlineEffectMaterial, 0);
                cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(0, m_SamplerArea, 0, 0));
                Blitter.BlitCameraTexture(cmd, m_TempRTHandle1, m_TempRTHandle2, m_OutlineEffectMaterial, 0);

                for (int i = 0; i < m_Iteration; i++)
                {
                    cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(m_SamplerArea, 0, 0, 0));
                    Blitter.BlitCameraTexture(cmd, m_TempRTHandle2, m_TempRTHandle1, m_OutlineEffectMaterial, 0);
                    cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(0, m_SamplerArea, 0, 0));
                    Blitter.BlitCameraTexture(cmd, m_TempRTHandle1, m_TempRTHandle2, m_OutlineEffectMaterial, 0);
                }

                Blitter.BlitCameraTexture(cmd, m_TempRTHandle2, m_Output, m_OutlineEffectMaterial, 1);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            m_TempRTHandle1?.Release();
            m_TempRTHandle2?.Release();
        }
    }
}