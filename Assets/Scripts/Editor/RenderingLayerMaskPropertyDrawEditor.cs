// /************************************************************
// FileName: RenderingLayerMaskPropertyDrawEdtior.cs
// Author: ZJL
// Date:2026-2-6 13:50
// Description: ~
// History: // 历史修改记录，至少保留最近7天记录
// <author>	<time>					<desc>
// ***********************************************************/

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskAttribute))]
public class RenderingLayerMaskPropertyDrawEditor : PropertyDrawer
{
    private static string[] m_DefaultRenderingLayerNames;

    internal static string[] defaultRenderingLayerNames
    {
        get
        {
            if (m_DefaultRenderingLayerNames == null)
            {
                m_DefaultRenderingLayerNames = new string[32];
                for (int i = 0; i < m_DefaultRenderingLayerNames.Length; ++i)
                {
                    m_DefaultRenderingLayerNames[i] = string.Format("Layer{0}", i + 1);
                }
            }

            return m_DefaultRenderingLayerNames;
        }
    }

    private static string[] m_DefaultPrefixedRenderingLayerNames;

    internal static string[] defaultPrefixedRenderingLayerNames
    {
        get
        {
            if (m_DefaultPrefixedRenderingLayerNames == null)
            {
                m_DefaultPrefixedRenderingLayerNames = new string[32];
                for (int i = 0; i < m_DefaultPrefixedRenderingLayerNames.Length; ++i)
                {
                    m_DefaultPrefixedRenderingLayerNames[i] = string.Format("{0}: {1}", i, defaultRenderingLayerNames[i]);
                }
            }

            return m_DefaultPrefixedRenderingLayerNames;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        DrawRenderingLayer(position, property, label);
        EditorGUI.EndProperty();
    }

    internal static void DrawRenderingLayer(Rect position, SerializedProperty layerMask, GUIContent label)
    {
        RenderPipelineAsset srpAsset = GraphicsSettings.currentRenderPipeline;
        bool usingSRP = srpAsset != null;
        if (!usingSRP)
            return;

        EditorGUI.showMixedValue = layerMask.hasMultipleDifferentValues;

        var mask = (int)layerMask.uintValue;
        var layerNames = srpAsset.prefixedRenderingLayerMaskNames;
        if (layerNames == null)
            layerNames = defaultPrefixedRenderingLayerNames;

        mask = EditorGUI.MaskField(position, label, mask, layerNames);
        layerMask.uintValue = (uint)mask;

        EditorGUI.showMixedValue = false;
    }
}