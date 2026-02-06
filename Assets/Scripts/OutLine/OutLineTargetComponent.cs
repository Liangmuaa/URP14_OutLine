using System;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class OutLineTargetComponent : MonoBehaviour
{
    private static int s_OutlineColorShaderId = Shader.PropertyToID("_EmissionColor");
    [SerializeField] private Renderer[] m_MeshRenderers;

    [SerializeField] [ColorUsage(true, true)]
    private Color m_OutlineColor = Color.cyan;

    public Color OutlineColor => m_OutlineColor;


    public virtual Renderer[] MeshRenderers
    {
        get { return m_MeshRenderers; }

        set { m_MeshRenderers = value; }
    }

    protected virtual void Awake()
    {
        if (m_MeshRenderers == null)
        {
            m_MeshRenderers = GetComponentsInChildren<Renderer>();
            m_MeshRenderers = m_MeshRenderers.Where(t => (!t.GetComponent<TMPro.TMP_Text>() && !t.GetComponent<TMPro.TMP_SubMesh>())).ToArray();
        }
    }

    public void AddTarget(Camera outlineCamera, Color color)
    {
        if (outlineCamera != null && outlineCamera.TryGetComponent(out OutLineCameraComponent outLineCameraComponent))
        {
            m_OutlineColor = color;
            outLineCameraComponent.AddTarget(this);
        }
    }

    public void RemoveTarget(Camera outlineCamera)
    {
        if (outlineCamera != null && outlineCamera.TryGetComponent(out OutLineCameraComponent outLineCameraComponent))
        {
            outLineCameraComponent.RemoveTarget(this);
        }
    }
}