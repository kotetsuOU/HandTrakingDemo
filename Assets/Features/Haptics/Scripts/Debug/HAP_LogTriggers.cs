using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

namespace Features.Haptics.Debug
{
    /// <summary>
    /// Haptics (HAP) モジュールの AppLogManager 連動ログトリガー定義および登録を担当するヘルパーコンポーネント。
    /// HAP_AUTDHapticsController コア本体から AppLogManager 登録処理を分離します。
    /// </summary>
    [AppLoggable("Haptics")]
    [DisallowMultipleComponent]
    public class HAP_LogTriggers : MonoBehaviour, IAppLoggable
    {
        public const string TagController = "HAP_Controller";
        public const string TagLinkService = "HAP_LinkService";
        public const string TagModulationService = "HAP_ModulationService";
        public const string TagTransformLoader = "HAP_TransformLoader";
        public const string TagCalibration = "HAP_Calibration";
        public const string TagPerformanceProfiler = "HAP_PerformanceProfiler";
        public const string TagSDKSetup = "HAP_SDKSetup";

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var controller = GetComponent<HAP_AUTDHapticsController>() ?? FindFirstObjectByType<HAP_AUTDHapticsController>();
            Object targetObj = controller != null ? (Object)controller : this;

            AddSubTriggerIfNotExists(group, targetObj, "[HAP_Controller] Main Controller & Dispatcher", TagController, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HAP_LinkService] AUTD3 Link Connection", TagLinkService, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HAP_ModulationService] Modulation & Silencer Control", TagModulationService, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HAP_TransformLoader] Transform & Snapshot Loader", TagTransformLoader, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HAP_Calibration] Device Alignment Calibration", TagCalibration, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HAP_PerformanceProfiler] Performance Profiler Log", TagPerformanceProfiler, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HAP_SDKSetup] AUTD3 SDK Symbol & Build Setup", TagSDKSetup, existingLabels);
        }

        private void AddSubTriggerIfNotExists(LogCategoryGroup group, Object targetObj, string label, string tag, HashSet<string> existingLabels)
        {
            if (!existingLabels.Contains(label))
            {
                group.entries.Add(new LogInstanceEntry
                {
                    label = label,
                    tag = tag,
                    target = targetObj,
                    enabled = true
                });
                existingLabels.Add(label);
            }
        }
    }
}
