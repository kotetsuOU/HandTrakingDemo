using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

namespace Features.Experiment.Debug
{
    /// <summary>
    /// Experiment モジュールの AppLogManager 連動ログトリガー定義および登録を担当するヘルパーコンポーネント。
    /// EXP_ExperimentManager コア本体から AppLogManager 登録処理を分離します。
    /// </summary>
    [AppLoggable("Experiment")]
    [DisallowMultipleComponent]
    public class EXP_LogTriggers : MonoBehaviour, IAppLoggable
    {
        public const string TagManager = "EXP_Manager";
        public const string TagFlowController = "EXP_FlowController";
        public const string TagTrialSequencer = "EXP_TrialSequencer";
        public const string TagInputHandler = "EXP_InputHandler";
        public const string TagEventMarker = "EXP_EventMarker";
        public const string TagDataRecorder = "EXP_DataRecorder";

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var manager = GetComponent<EXP_ExperimentManager>() ?? FindFirstObjectByType<EXP_ExperimentManager>();
            Object targetObj = manager != null ? (Object)manager : this;

            AddSubTriggerIfNotExists(group, targetObj, "[EXP_Manager] State & Flow Manager", TagManager, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[EXP_FlowController] Main Loop & Trial Runner", TagFlowController, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[EXP_TrialSequencer] Sequence Generation", TagTrialSequencer, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[EXP_InputHandler] Participant Response Input", TagInputHandler, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[EXP_EventMarker] Event Timestamp Logging", TagEventMarker, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[EXP_DataRecorder] Data Output File Storage", TagDataRecorder, existingLabels);
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
