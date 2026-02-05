using UnityEngine;
using System.Collections.Generic;

namespace SVFramework
{
    public class OutLineCameraComponent : MonoBehaviour
    {
        [Header("采样范围")] public float SamplerArea = 0.001f;

        [Header("降分辨率")] [Range(0, 2)] public int DownSample = 0;

        [Header("迭代次数")] [Range(1, 4)] public int Iteration = 1;

        [Header("描边强度")] [Range(0.0f, 10.0f)] public float OutLineStrength = 1.0f;

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

            m_TargetObjects.Add(target);
        }

        public void RemoveTarget(OutLineTargetComponent target)
        {
            m_TargetObjects.Remove(target);
        }
    }
}