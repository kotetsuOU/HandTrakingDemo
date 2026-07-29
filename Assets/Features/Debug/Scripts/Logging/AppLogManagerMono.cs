// =============================================================================
// AppLogManagerMono.cs
// -----------------------------------------------------------------------------
// AppLogManager をインスペクターから視覚的に一斉制御・ON/OFFするためのコンポーネント。
// シーン内の GameObject にアタッチするか、デバッグ用に配置して使用します。
// =============================================================================

using UnityEngine;

namespace RealTimeOcclusion.Logging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RealTimeOcclusion/Debug/App Log Manager")]
    public class AppLogManagerMono : MonoBehaviour
    {
        [Header("Global Settings")]
        [Tooltip("全体ログマスター切り替え")]
        [SerializeField] private bool _globalEnableLog = true;

        [Header("Category Settings")]
        [SerializeField] private bool _enableBufferManager = true;
        [SerializeField] private bool _enableOcclusion = true;
        [SerializeField] private bool _enableRealSense = true;
        [SerializeField] private bool _enableHaptics = true;
        [SerializeField] private bool _enableExperiment = true;

        private void Awake()
        {
            ApplySettings();
        }

        private void OnValidate()
        {
            ApplySettings();
        }

        public void ApplySettings()
        {
            AppLogManager.GlobalEnableLog = _globalEnableLog;
            AppLogManager.SetCategoryEnabled(AppLogCategory.BufferManager, _enableBufferManager);
            AppLogManager.SetCategoryEnabled(AppLogCategory.Occlusion, _enableOcclusion);
            AppLogManager.SetCategoryEnabled(AppLogCategory.RealSense, _enableRealSense);
            AppLogManager.SetCategoryEnabled(AppLogCategory.Haptics, _enableHaptics);
            AppLogManager.SetCategoryEnabled(AppLogCategory.Experiment, _enableExperiment);
        }
    }
}
