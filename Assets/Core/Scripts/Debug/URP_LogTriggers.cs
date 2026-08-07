using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

namespace Core.Debug
{
    /// <summary>
    /// URP / RenderPipelines 関連の AppLogManager 連動ログトリガー定義および登録を担当するコンポーネント。
    /// </summary>
    [AppLoggable("URP / RenderPipelines")]
    [DisallowMultipleComponent]
    public class URP_LogTriggers : MonoBehaviour, IAppLoggable
    {
        public const string TagMatrixDebug = "URP_MatrixDebug";

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var matrixDebugFeature = FindFirstObjectByType<URPMatrixDebugFeature>();
            Object targetObj = matrixDebugFeature != null ? (Object)matrixDebugFeature : this;

            AddSubTriggerIfNotExists(group, targetObj, "[URP_MatrixDebug] URP Pass State & Matrix Diagnostics", TagMatrixDebug, existingLabels);
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
