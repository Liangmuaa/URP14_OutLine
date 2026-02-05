// /************************************************************
// FileName: OutlineRenderPass.cs
// Author: ZJL
// Date:2026-1-22 10:0
// Description: 渲染需要描边物体
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SVFramework.URP
{
    public class OutlineRenderPass_PreDrawOutlineObjects : ScriptableRenderPass
    {
        public enum DrawMode
        {
            Renderer,
            Mesh,
            MeshInstance  //不要使用这个模式, 没实现完整
        }

        private class OutlineRenderData
        {
            public Mesh Mesh;
            public int SubMeshIndex;
            public Matrix4x4 LocalToWorldMatrix;
            public Color OutlineColor;
        }

        private static readonly int s_ColorShaderId = Shader.PropertyToID("_Color");
        private static readonly int s_PreOutlineTextureShaderId = Shader.PropertyToID("_PreOutlineTexture");
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("OutlineRenderPass_PreDrawOutlineObjects");

        private readonly DrawMode m_Mode;
        private readonly Material m_PreOutlineMaterial;
        private readonly List<OutlineRenderData> m_RenderDataList;
        private readonly MaterialPropertyBlock m_PropertyBlock;


        private OutLineCameraComponent m_OutLineCameraContainer;
        private RTHandle m_Output;


        public OutlineRenderPass_PreDrawOutlineObjects(RenderPassEvent evt, DrawMode mode, Material preOutlineMat)
        {
            renderPassEvent = evt;
            m_Mode = mode;
            m_PreOutlineMaterial = preOutlineMat;
            m_RenderDataList = new List<OutlineRenderData>();
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        public void Setup(OutLineCameraComponent outLineCameraContainer, ref RTHandle dest)
        {
            m_OutLineCameraContainer = outLineCameraContainer;
            m_Output = dest;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            if (m_Output == null)
            {
                return;
            }

            ConfigureTarget(m_Output);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        private void CollectRenderData()
        {
            m_RenderDataList.Clear();
            if (m_OutLineCameraContainer == null || m_OutLineCameraContainer.TargetObjects == null)
            {
                return;
            }

            if (m_Output == null)
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

        private void DrawRenderers(CommandBuffer cmd)
        {
            for (int i = 0; i < m_OutLineCameraContainer.TargetObjects.Count; i++)
            {
                var target = m_OutLineCameraContainer.TargetObjects[i];
                if (target == null)
                {
                    continue;
                }

                if (!target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var renderers = target.MeshRenderers;
                if (renderers == null || renderers.Length == 0)
                    continue;


                m_PropertyBlock.SetColor(s_ColorShaderId, target.OutlineColor);
                for (int j = 0; j < renderers.Length; j++)
                {
                    var renderer = renderers[j];
                    var sharedMaterials = renderer.sharedMaterials;
                    for (int k = 0; k < sharedMaterials.Length; k++)
                    {
                        renderer.SetPropertyBlock(m_PropertyBlock);
                        cmd.DrawRenderer(renderer, m_PreOutlineMaterial, k, 0);
                    }
                }
            }
        }

        private void Draw(CommandBuffer cmd)
        {
            switch (m_Mode)
            {
                case DrawMode.Renderer:
                    DrawRenderers(cmd);
                    break;
                case DrawMode.Mesh:
                    DrawMeshes(cmd);
                    break;
                case DrawMode.MeshInstance:
                    DrawMeshInstances(cmd);
                    break;
            }
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
                Draw(cmd);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
        }
    }
}