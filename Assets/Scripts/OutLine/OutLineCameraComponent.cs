using UnityEngine;
using System.Collections.Generic;

public class OutLineCameraComponent : MonoBehaviour
{
    [Header("采样范围"), Range(0.001f, 0.005f)]
    public float SamplerArea = 0.001f;

    [Header("降分辨率")] [Range(0, 2)] public int DownSample = 0;

    [Header("迭代次数")] [Range(1, 4)] public int Iteration = 1;

    [Header("描边强度")] [Range(0.0f, 10.0f)] public float OutLineStrength = 1.0f;

    [RenderingLayerMask] public int RenderingMask = 1 << 1;

    [SerializeField] [ColorUsage(true, true)]
    private Color m_OutlineColor = Color.cyan;

    public Color OutlineColor => m_OutlineColor;

    //目标对象  
    [SerializeField] private List<OutLineTargetComponent> m_TargetObjects = new List<OutLineTargetComponent>();

    public List<OutLineTargetComponent> TargetObjects => m_TargetObjects;

    private void OnEnable()
    {
    }

    public void AddTarget(OutLineTargetComponent target)
    {
        if (m_TargetObjects.Contains(target))
        {
            return;
        }

        if (target.MeshRenderers != null)
        {
            foreach (var meshRenderer in target.MeshRenderers)
            {
                meshRenderer.renderingLayerMask |= (uint)RenderingMask;
            }
        }

        m_TargetObjects.Add(target);
    }

    public void RemoveTarget(OutLineTargetComponent target)
    {
        m_TargetObjects.Remove(target);

        if (target.MeshRenderers != null)
        {
            foreach (var meshRenderer in target.MeshRenderers)
            {
                meshRenderer.renderingLayerMask &= ~(uint)RenderingMask;
            }
        }
    }
}