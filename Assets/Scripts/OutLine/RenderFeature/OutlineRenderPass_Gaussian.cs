// /************************************************************
// FileName: OutlineRenderPass_Gaussian.cs
// Author: ZJL
// Date:2026-2-6 15:10
// Description: 高斯模糊描边
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineRenderPass_Gaussian : ScriptableRenderPass
{
    private class OutlineRenderData
    {
        public Mesh Mesh;
        public int SubMeshIndex;
        public Matrix4x4 LocalToWorldMatrix;
        public Color OutlineColor;
    }


    private const string k_PreDrawObjectsTexName = "_PreDrawObjectsTexture";
    private const string k_BlurOutlineTexName = "_BlurOutlineTexture";
    private static readonly ShaderTagId s_ShaderTagId = new ShaderTagId("OutLine");
    private static readonly int s_ColorShaderId = Shader.PropertyToID("_Color");
    private static readonly int s_OffsetsShaderId = Shader.PropertyToID("_Offsets");
    private static readonly int s_OutlineStrengthShaderId = Shader.PropertyToID("_OutlineStrength");
    private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("OutlineRenderPass_Goss");


    private readonly OutlineRenderFeature.DrawMode m_Mode;
    private readonly Material m_PreOutlineMaterial;
    private readonly Material m_OutlineEffectMaterial;
    private readonly List<OutlineRenderData> m_RenderDataList;
    private readonly MaterialPropertyBlock m_PropertyBlock;
    private readonly FilteringSettings m_FilteringSettings;

    private OutLineCameraComponent m_OutLineCameraContainer;

    private RTHandle m_PreDrawObjectsRTHandle;
    private RTHandle m_BlurOutlineRTHandle;
    private RTHandle m_TempRTHandle1;
    private RTHandle m_TempRTHandle2;


    private float m_SamplerArea;
    private int m_Iteration;
    private int m_DownSample;
    private float m_OutlineStrength;

    public OutlineRenderPass_Gaussian(RenderPassEvent evt, Material preOutlineMaterial, Material outlineEffectMaterial, OutlineRenderFeature.DrawMode drawMode, uint renderingLayerMask)
    {
        renderPassEvent = evt;
        m_PreOutlineMaterial = preOutlineMaterial;
        m_OutlineEffectMaterial = outlineEffectMaterial;
        m_RenderDataList = new List<OutlineRenderData>();
        m_PropertyBlock = new MaterialPropertyBlock();
        m_Mode = drawMode;
        m_FilteringSettings = new FilteringSettings(renderingLayerMask: renderingLayerMask);
    }

    public void Setup(OutLineCameraComponent outLineCameraComponent)
    {
        m_OutLineCameraContainer = outLineCameraComponent;
        m_SamplerArea = outLineCameraComponent.SamplerArea;
        m_Iteration = outLineCameraComponent.Iteration;
        m_DownSample = outLineCameraComponent.DownSample;
        m_OutlineStrength = outLineCameraComponent.OutLineStrength;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        var downSampleDesc = cameraTextureDescriptor;
        downSampleDesc.width >>= m_DownSample;
        downSampleDesc.height >>= m_DownSample;
        downSampleDesc.depthBufferBits = 0;
        downSampleDesc.msaaSamples = 1;
        downSampleDesc.colorFormat = RenderTextureFormat.ARGB32;

        RenderingUtils.ReAllocateIfNeeded(ref m_PreDrawObjectsRTHandle, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_PreDrawObjectsTexName);
        RenderingUtils.ReAllocateIfNeeded(ref m_BlurOutlineRTHandle, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: k_BlurOutlineTexName);

        downSampleDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
        RenderingUtils.ReAllocateIfNeeded(ref m_TempRTHandle1, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp);
        RenderingUtils.ReAllocateIfNeeded(ref m_TempRTHandle2, downSampleDesc, FilterMode.Bilinear, TextureWrapMode.Clamp);


        ResetTarget();
    }


    private void CollectRenderData()
    {
        m_RenderDataList.Clear();
        if (m_OutLineCameraContainer == null || m_OutLineCameraContainer.TargetObjects == null)
        {
            return;
        }

        for (int i = 0; i < m_OutLineCameraContainer.TargetObjects.Count; i++)
        {
            var target = m_OutLineCameraContainer.TargetObjects[i];
            if (target == null)
                continue;
            var renderers = target.MeshRenderers;
            if (renderers == null || renderers.Length == 0)
                continue;

            for (int j = 0; j < renderers.Length; j++)
            {
                var renderer = renderers[j];
                if (renderer == null || !renderer.enabled)
                    continue;

                Mesh mesh = null;
                if (renderer is MeshRenderer)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        continue;
                    mesh = meshFilter.sharedMesh;
                }
                else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    mesh = skinnedMeshRenderer.sharedMesh;
                }

                if (mesh == null)
                    continue;
                var sharedMaterials = renderer.sharedMaterials;
                for (int k = 0; k < sharedMaterials.Length; k++)
                {
                    var data = new OutlineRenderData()
                    {
                        Mesh = mesh,
                        SubMeshIndex = k,
                        OutlineColor = target.OutlineColor,
                        LocalToWorldMatrix = renderer.localToWorldMatrix
                    };
                    m_RenderDataList.Add(data);
                }
            }
        }
    }

    private void DrawMeshes(CommandBuffer cmd)
    {
        CollectRenderData();

        foreach (var renderData in m_RenderDataList)
        {
            m_PropertyBlock.SetColor(s_ColorShaderId, renderData.OutlineColor);
            cmd.DrawMesh(renderData.Mesh, renderData.LocalToWorldMatrix, m_PreOutlineMaterial, renderData.SubMeshIndex, 0, m_PropertyBlock);
        }
    }

    private void DrawMeshInstances(CommandBuffer cmd)
    {
        CollectRenderData();

        const int k_Max_Instance_Count = 1023;

        var meshGroup = new Dictionary<(Mesh, int), List<Matrix4x4>>();

        foreach (var renderData in m_RenderDataList)
        {
            var key = (renderData.Mesh, renderData.SubMeshIndex);
            if (!meshGroup.TryGetValue(key, out var matrices))
            {
                matrices = new List<Matrix4x4>();
                meshGroup[key] = matrices;
            }

            matrices.Add(renderData.LocalToWorldMatrix);
        }

        foreach (var group in meshGroup)
        {
            var mesh = group.Key.Item1;
            var subMeshIndex = group.Key.Item2;
            var instanceMatrices = group.Value;
            for (int i = 0; i < instanceMatrices.Count; i += k_Max_Instance_Count)
            {
                int count = Mathf.Min(k_Max_Instance_Count, instanceMatrices.Count - i);
                var batchMatrices = instanceMatrices.GetRange(i, count);
                cmd.DrawMeshInstanced(mesh, subMeshIndex, m_PreOutlineMaterial, 0, batchMatrices.ToArray(), count, m_PropertyBlock);
            }
        }
    }

    private void DrawRenderers(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd)
    {
        // for (int i = 0; i < m_OutLineCameraContainer.TargetObjects.Count; i++)
        // {
        //     var target = m_OutLineCameraContainer.TargetObjects[i];
        //     if (target == null)
        //     {
        //         continue;
        //     }
        //
        //     if (!target.gameObject.activeInHierarchy)
        //     {
        //         continue;
        //     }
        //
        //     var renderers = target.MeshRenderers;
        //     if (renderers == null || renderers.Length == 0)
        //         continue;
        //
        //
        //     m_PropertyBlock.SetColor(s_ColorShaderId, target.OutlineColor);
        //     for (int j = 0; j < renderers.Length; j++)
        //     {
        //         var renderer = renderers[j];
        //         var sharedMaterials = renderer.sharedMaterials;
        //         for (int k = 0; k < sharedMaterials.Length; k++)
        //         {
        //             renderer.SetPropertyBlock(m_PropertyBlock);
        //             cmd.DrawRenderer(renderer, m_PreOutlineMaterial, k, 0);
        //         }
        //     }
        // }


        var drawSettings = CreateDrawingSettings(s_ShaderTagId, ref renderingData, SortingCriteria.None);
        RendererListParams renderParams = new RendererListParams();
        renderParams.drawSettings = drawSettings;
        renderParams.cullingResults = renderingData.cullResults;
        renderParams.filteringSettings = m_FilteringSettings;
        var rendererList = context.CreateRendererList(ref renderParams);
        cmd.DrawRendererList(rendererList);
    }

    private void Draw(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd)
    {
        cmd.SetRenderTarget(m_PreDrawObjectsRTHandle);
        cmd.ClearRenderTarget(true, true, Color.clear);
        switch (m_Mode)
        {
            case OutlineRenderFeature.DrawMode.Renderer:
                DrawRenderers(context, ref renderingData, cmd);
                break;
            case OutlineRenderFeature.DrawMode.Mesh:
                DrawMeshes(cmd);
                break;
            case OutlineRenderFeature.DrawMode.MeshInstance:
                DrawMeshInstances(cmd);
                break;
        }
    }

    private void Goss(CommandBuffer cmd)
    {
        cmd.SetRenderTarget(m_TempRTHandle1);
        cmd.ClearRenderTarget(true, true, Color.clear);
        cmd.SetRenderTarget(m_TempRTHandle2);
        cmd.ClearRenderTarget(true, true, Color.clear);
        cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(m_SamplerArea, 0, 0, 0));
        Blitter.BlitCameraTexture(cmd, m_PreDrawObjectsRTHandle, m_TempRTHandle1, m_OutlineEffectMaterial, 0);
        cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(0, m_SamplerArea, 0, 0));
        Blitter.BlitCameraTexture(cmd, m_TempRTHandle1, m_TempRTHandle2, m_OutlineEffectMaterial, 0);

        for (int i = 0; i < m_Iteration; i++)
        {
            cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(m_SamplerArea, 0, 0, 0));
            Blitter.BlitCameraTexture(cmd, m_TempRTHandle2, m_TempRTHandle1, m_OutlineEffectMaterial, 0);
            cmd.SetGlobalVector(s_OffsetsShaderId, new Vector4(0, m_SamplerArea, 0, 0));
            Blitter.BlitCameraTexture(cmd, m_TempRTHandle1, m_TempRTHandle2, m_OutlineEffectMaterial, 0);
        }

        m_OutlineEffectMaterial.SetTexture(m_PreDrawObjectsRTHandle.name, m_PreDrawObjectsRTHandle);
        Blitter.BlitCameraTexture(cmd, m_TempRTHandle2, m_BlurOutlineRTHandle, m_OutlineEffectMaterial, 1);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if (cameraData.cameraType != CameraType.Game)
            return;

        if (m_PreOutlineMaterial == null)
        {
            return;
        }

        if (m_OutLineCameraContainer == null)
        {
            return;
        }

        if (m_OutLineCameraContainer.TargetObjects == null || m_OutLineCameraContainer.TargetObjects.Count == 0)
        {
            return;
        }


        var cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            //先画图
            Draw(context, ref renderingData, cmd);
            //高斯模糊,裁剪出外轮廓
            Goss(cmd);
            //合并
            m_OutlineEffectMaterial.SetTexture(m_BlurOutlineRTHandle.name, m_BlurOutlineRTHandle);
            m_OutlineEffectMaterial.SetFloat(s_OutlineStrengthShaderId, m_OutlineStrength);
            Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, cameraData.renderer.cameraColorTargetHandle, m_OutlineEffectMaterial, 2); //相加
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public void Dispose()
    {
        m_PreDrawObjectsRTHandle?.Release();
        m_BlurOutlineRTHandle?.Release();
        m_TempRTHandle1?.Release();
        m_TempRTHandle2?.Release();
    }
}