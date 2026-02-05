// /************************************************************
// FileName: OutlineRenderFeature.cs
// Author: ZJL
// Date:2026-1-22 9:59
// Description: ~
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SVFramework.URP
{
    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        [Header("Outline/OutlinePrePass.shader")]
        public Shader PreoutlineShader;

        [Header("Outline/OutLineEffect.shader")]
        public Shader OutlineShader;

        public OutlineRenderPass_PreDrawOutlineObjects.DrawMode DrawMode = OutlineRenderPass_PreDrawOutlineObjects.DrawMode.Mesh;
        public RenderPassEvent RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private OutlineRenderPass_CopyColor m_OutlineRenderCopyColorPass;
        private OutlineRenderPass_PreDrawOutlineObjects m_OutlineRenderPreDrawOutlineObjectsPass;
        private OutlineRenderPass_BlurOutlineObjects m_OutlineRenderBlurOutlineObjectsPass;
        private OutlineRenderPass_FinalCombine m_OutlineRenderFinalCombinePass;
        private Material m_OutlineEffectMaterial;
        private Material m_PreOutlineMaterial;

        private RTHandle m_CopyColorRTHandle;
        private const string k_CopyColorTexName = "_OutlineOriginColorTexture";
        private RTHandle m_PreDrawObjectsRTHandle;
        private const string k_PreDrawObjectsTexName = "_PreDrawObjectsTexture";
        private RTHandle m_BlurOutlineRTHandle;
        private const string k_BlurOutlineTexName = "_BlurOutlineTexture";

        public override void Create()
        {
            if (OutlineShader == null)
            {
                return;
            }

            if (PreoutlineShader == null)
            {
                return;
            }

            m_PreOutlineMaterial = CoreUtils.CreateEngineMaterial(PreoutlineShader);
            m_OutlineEffectMaterial = CoreUtils.CreateEngineMaterial(OutlineShader);
            m_PreOutlineMaterial.enableInstancing = true;

            m_OutlineRenderCopyColorPass = new OutlineRenderPass_CopyColor(RenderPassEvent);
            m_OutlineRenderPreDrawOutlineObjectsPass = new OutlineRenderPass_PreDrawOutlineObjects(RenderPassEvent, DrawMode, m_PreOutlineMaterial);
            m_OutlineRenderBlurOutlineObjectsPass = new OutlineRenderPass_BlurOutlineObjects(RenderPassEvent, m_OutlineEffectMaterial);
            m_OutlineRenderFinalCombinePass = new OutlineRenderPass_FinalCombine(RenderPassEvent, m_OutlineEffectMaterial);
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

            renderer.EnqueuePass(m_OutlineRenderCopyColorPass);
            renderer.EnqueuePass(m_OutlineRenderPreDrawOutlineObjectsPass);
            renderer.EnqueuePass(m_OutlineRenderBlurOutlineObjectsPass);
            renderer.EnqueuePass(m_OutlineRenderFinalCombinePass);
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

            var downSampleDesc = renderingData.cameraData.cameraTargetDescriptor;
            downSampleDesc.width >>= cameraOutlineContainer.DownSample;
            downSampleDesc.height >>= cameraOutlineContainer.DownSample;
            downSampleDesc.depthBufferBits = 0;
            downSampleDesc.msaaSamples = 1;
            downSampleDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;

            var desc = renderingData.cameraData.cameraTargetDescriptor; //CopyColor使用原图画质
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;


            RenderingUtils.ReAllocateIfNeeded(ref m_CopyColorRTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_CopyColorTexName);
            RenderingUtils.ReAllocateIfNeeded(ref m_PreDrawObjectsRTHandle, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_PreDrawObjectsTexName);
            RenderingUtils.ReAllocateIfNeeded(ref m_BlurOutlineRTHandle, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_BlurOutlineTexName);


            m_OutlineRenderCopyColorPass.Setup(renderer.cameraColorTargetHandle, ref m_CopyColorRTHandle);
            m_OutlineRenderPreDrawOutlineObjectsPass.Setup(cameraOutlineContainer, ref m_PreDrawObjectsRTHandle);
            m_OutlineRenderBlurOutlineObjectsPass.Setup(ref m_PreDrawObjectsRTHandle, ref m_BlurOutlineRTHandle, cameraOutlineContainer.SamplerArea, cameraOutlineContainer.Iteration, cameraOutlineContainer.DownSample);
            m_OutlineRenderFinalCombinePass.Setup(ref m_CopyColorRTHandle, ref m_BlurOutlineRTHandle, renderer.cameraColorTargetHandle, cameraOutlineContainer.OutLineStrength);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            CoreUtils.Destroy(m_OutlineEffectMaterial);
            m_OutlineEffectMaterial = null;
            CoreUtils.Destroy(m_PreOutlineMaterial);
            m_PreOutlineMaterial = null;

            m_OutlineRenderPreDrawOutlineObjectsPass?.Dispose();
            m_OutlineRenderBlurOutlineObjectsPass?.Dispose();
            m_OutlineRenderFinalCombinePass?.Dispose();


            m_OutlineRenderCopyColorPass = null;
            m_OutlineRenderPreDrawOutlineObjectsPass = null;
            m_OutlineRenderBlurOutlineObjectsPass = null;
            m_OutlineRenderFinalCombinePass = null;

            m_CopyColorRTHandle?.Release();
            m_PreDrawObjectsRTHandle?.Release();
            m_BlurOutlineRTHandle?.Release();
        }
    }
}