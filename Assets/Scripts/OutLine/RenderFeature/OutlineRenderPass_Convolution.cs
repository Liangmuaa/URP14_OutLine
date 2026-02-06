// /************************************************************
// FileName: OutlineRenderPass_Convolution.cs
// Author: ZJL
// Date:2026-2-5 18:19
// Description: 卷积描边
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineRenderPass_Convolution : ScriptableRenderPass
{
    private const string k_PreDrawObjectsTexName = "_PreDrawObjectsTexture";
    private static readonly ShaderTagId s_ShaderTagId = new ShaderTagId("OutLine");
    private static readonly int s_PreOutlineTextureShaderId = Shader.PropertyToID(k_PreDrawObjectsTexName);
    private static readonly int s_OutlineWidthShaderId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int s_OutlineColorShaderId = Shader.PropertyToID("_OutlineColor");
    private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("OutlineRenderPass_ConvolutionOutline");

    private readonly Material m_OutlineMaterial;

    private RTHandle m_OutlineTargetHandle;
    private float m_OutlineWidth;
    private Color m_OutlineColor;
    private int m_DownSample;

    private readonly MaterialPropertyBlock m_PropertyBlock;

    private FilteringSettings m_FilteringSettings;


    public OutlineRenderPass_Convolution(RenderPassEvent evt, Material mat, uint layerMask)
    {
        renderPassEvent = evt;
        m_OutlineMaterial = mat;
        m_PropertyBlock = new MaterialPropertyBlock();
        m_FilteringSettings = new FilteringSettings(renderingLayerMask: layerMask);
    }

    public void Setup(float outlineWidth, Color outlineColor, int downSample)
    {
        m_OutlineWidth = outlineWidth;
        m_OutlineColor = outlineColor;

        m_DownSample = downSample;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        var downSampleDesc = cameraTextureDescriptor;
        downSampleDesc.colorFormat = RenderTextureFormat.ARGB32;
        downSampleDesc.width >>= m_DownSample;
        downSampleDesc.height >>= m_DownSample;
        downSampleDesc.depthBufferBits = 0;
        downSampleDesc.msaaSamples = 1;

        RenderingUtils.ReAllocateIfNeeded(ref m_OutlineTargetHandle, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_PreDrawObjectsTexName);

        ResetTarget();
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if (cameraData.cameraType != CameraType.Game)
            return;

        var cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            cmd.SetRenderTarget(m_OutlineTargetHandle);
            cmd.ClearRenderTarget(true, true, Color.clear);

            var drawSettings = CreateDrawingSettings(s_ShaderTagId, ref renderingData, SortingCriteria.None);
            RendererListParams renderParams = new RendererListParams();
            renderParams.drawSettings = drawSettings;
            renderParams.cullingResults = renderingData.cullResults;
            renderParams.filteringSettings = m_FilteringSettings;
            var rendererList = context.CreateRendererList(ref renderParams);
            cmd.DrawRendererList(rendererList);

            cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            m_PropertyBlock.SetTexture(s_PreOutlineTextureShaderId, m_OutlineTargetHandle);
            m_PropertyBlock.SetFloat(s_OutlineWidthShaderId, m_OutlineWidth);
            m_PropertyBlock.SetColor(s_OutlineColorShaderId, m_OutlineColor);
            cmd.DrawProcedural(Matrix4x4.identity, m_OutlineMaterial, 3, MeshTopology.Triangles, 3, 1, m_PropertyBlock);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        m_OutlineTargetHandle?.Release();
    }
}