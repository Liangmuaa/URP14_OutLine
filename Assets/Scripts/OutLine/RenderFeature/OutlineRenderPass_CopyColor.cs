// /************************************************************
// FileName: OutlineRenderPass_CopyColor.cs
// Author: ZJL
// Date:2026-1-23 15:1
// Description: 拷贝原色
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SVFramework.URP
{
    public class OutlineRenderPass_CopyColor : ScriptableRenderPass
    {
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("OutlineRenderPass_CopyColor");
        private RTHandle m_Source;
        private RTHandle m_Output;

        public OutlineRenderPass_CopyColor(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public void Setup(RTHandle src, ref RTHandle dest)
        {
            m_Source = src;
            m_Output = dest;
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
                Blitter.BlitCameraTexture(cmd, m_Source, m_Output, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}