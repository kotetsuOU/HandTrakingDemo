using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

namespace Features.HapticsCollision.Debug
{
    /// <summary>
    /// HCD モジュールの AppLogManager 連動ログトリガー定義および登録を担当するヘルパーコンポーネント。
    /// HCD_Pipeline コア本体から AppLogManager 登録処理を分離します。
    /// </summary>
    [AppLoggable("HCD (Haptic Collision)")]
    [DisallowMultipleComponent]
    public class HCD_LogTriggers : MonoBehaviour, IAppLoggable
    {
        public const string TagPipeline = "HCD_Pipeline";
        public const string TagDistanceProcessor = "HCD_DistanceProcessor";
        public const string TagSpatialClusteringProcessor = "HCD_SpatialClusteringProcessor";
        public const string TagClusterTracker = "HCD_ClusterTracker";

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var pipeline = GetComponent<HCD_Pipeline>() ?? HCD_Pipeline.Instance;
            Object targetObj = pipeline != null ? (Object)pipeline : this;

            AddSubTriggerIfNotExists(group, targetObj, "[HCD_Pipeline] Summary & Readback", TagPipeline, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HCD_DistanceProcessor] Mesh & Bounds Debug", TagDistanceProcessor, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HCD_SpatialClusteringProcessor] Clustering Debug", TagSpatialClusteringProcessor, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[HCD_ClusterTracker] Cluster Tracking Info", TagClusterTracker, existingLabels);
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
