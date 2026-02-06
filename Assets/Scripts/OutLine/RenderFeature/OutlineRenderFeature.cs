// /************************************************************
// FileName: OutlineRenderFeature.cs
// Author: ZJL
// Date:2026-1-22 9:59
// Description: ~
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineRenderFeature : ScriptableRendererFeature
{
    public enum OutlineMode
    {
        Convolution,
        Gaussian,
    }

    public enum DrawMode
    {
        Renderer,
        Mesh,
        MeshInstance //不要使用这个模式, 没实现完整
    }

    [Header("Outline/OutlinePrePass.shader")]
    public Shader PreoutlineShader;

    [Header("Outline/OutLineEffect.shader")]
    public Shader OutlineShader;

    public OutlineMode Mode;

    public RenderPassEvent RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    [RenderingLayerMask] public uint RenderingMask = 1 << 1;

    private OutlineRenderPass_Convolution m_OutlineRenderConvolutionPass;
    private OutlineRenderPass_Gaussian m_OutlineRenderGaussianPass;
    private Material m_OutlineEffectMaterial;
    private Material m_PreOutlineMaterial;

    public override void Create()
    {
        if (OutlineShader == null)
        {
            return;
        }

        m_OutlineEffectMaterial = CoreUtils.CreateEngineMaterial(OutlineShader);

        switch (Mode)
        {
            case OutlineMode.Convolution:
                m_OutlineRenderConvolutionPass = new OutlineRenderPass_Convolution(RenderPassEvent, m_OutlineEffectMaterial, RenderingMask);
                break;
            case OutlineMode.Gaussian:
                if (PreoutlineShader == null)
                {
                    return;
                }

                m_PreOutlineMaterial = CoreUtils.CreateEngineMaterial(PreoutlineShader);
                m_OutlineRenderGaussianPass = new OutlineRenderPass_Gaussian(RenderPassEvent, m_PreOutlineMaterial, m_OutlineEffectMaterial, DrawMode.Mesh, RenderingMask);
                break;
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        var cameraOutlineContainer = renderingData.cameraData.camera.GetComponent<OutLineCameraComponent>();

        if (cameraOutlineContainer == null || !cameraOutlineContainer.enabled)
        {
            return;
        }

        if (cameraOutlineContainer.TargetObjects == null || cameraOutlineContainer.TargetObjects.Count == 0)
        {
            return;
        }

        switch (Mode)
        {
            case OutlineMode.Convolution:
                renderer.EnqueuePass(m_OutlineRenderConvolutionPass);
                break;
            case OutlineMode.Gaussian:
                renderer.EnqueuePass(m_OutlineRenderGaussianPass);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        if (renderer.cameraColorTargetHandle == null)
        {
            return;
        }

        var cameraOutlineContainer = renderingData.cameraData.camera.GetComponent<OutLineCameraComponent>();

        if (cameraOutlineContainer == null || !cameraOutlineContainer.enabled)
        {
            return;
        }


        if (cameraOutlineContainer.TargetObjects == null || cameraOutlineContainer.TargetObjects.Count == 0)
        {
            return;
        }

        switch (Mode)
        {
            case OutlineMode.Convolution:
                m_OutlineRenderConvolutionPass.Setup(cameraOutlineContainer.SamplerArea, cameraOutlineContainer.OutlineColor, cameraOutlineContainer.DownSample);

                break;
            case OutlineMode.Gaussian:
                m_OutlineRenderGaussianPass.Setup(cameraOutlineContainer);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        CoreUtils.Destroy(m_OutlineEffectMaterial);
        m_OutlineEffectMaterial = null;
        CoreUtils.Destroy(m_PreOutlineMaterial);
        m_PreOutlineMaterial = null;

        m_OutlineRenderConvolutionPass?.Dispose();
        m_OutlineRenderGaussianPass?.Dispose();
    }
}