// /************************************************************
// FileName: OutlinePostRenderPass.cs
// Author: ZJL
// Date:2026-1-22 17:44
// Description: 叠加原图像和描边
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SVFramework.URP
{
    public class OutlineRenderPass_FinalCombine : ScriptableRenderPass
    {
        public static readonly int s_OutlineStrengthShaderId = Shader.PropertyToID("_OutlineStrength");
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("OutlineRenderPass_FinalCombine");

        private Material m_OutlineEffectMaterial;

        private RTHandle m_Source;
        private RTHandle m_Output;

        public OutlineRenderPass_FinalCombine(RenderPassEvent evt, Material outlineMat)
        {
            renderPassEvent = evt;
            m_OutlineEffectMaterial = outlineMat;
        }

        public void Setup(ref RTHandle src, ref RTHandle blurOutlineRTHandle, RTHandle des, float outlineStrength)
        {
            m_Source = src;
            m_Output = des;
            m_OutlineEffectMaterial.SetTexture(blurOutlineRTHandle.name, blurOutlineRTHandle);
            m_OutlineEffectMaterial.SetFloat(s_OutlineStrengthShaderId, outlineStrength);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);


            if (m_Output == null)
            {
                return;
            }

            ConfigureTarget(m_Output);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            if (m_Output == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, m_Source, m_Output, m_OutlineEffectMaterial, 2); //相加
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            m_OutlineEffectMaterial = null;
        }
    }
}