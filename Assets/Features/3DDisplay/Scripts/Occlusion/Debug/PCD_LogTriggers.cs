using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

/// <summary>
/// PCD (Point Cloud Display / Occlusion) モジュールの AppLogManager 連動ログトリガー定義および登録を担当するコンポーネント。
/// 通常のパイプライン/バッファログと Record (データ記録・ファイル保存) デバッグログを個別分離して制御できるようにします。
/// </summary>
[AppLoggable("PCD (Occlusion)")]
[DisallowMultipleComponent]
public class PCD_LogTriggers : MonoBehaviour, IAppLoggable
{
    // 通常デバッグログ (Pipeline & Buffer Core)
    public const string TagPipeline = "PCD_Pipeline";
    public const string TagBuffer = "PCD_BufferManager";

    // Record デバッグログ (RecordDebug & Readback & Exporter)
    public const string TagRecordDebug = "PCD_RecordDebug";
    public const string TagExporter = "PCD_Exporter";

    public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
    {
        var controller = GetComponent<PCDOcclusionPipelineController>() ?? FindFirstObjectByType<PCDOcclusionPipelineController>();
        Object targetObj = controller != null ? (Object)controller : this;

        // 1. 通常デバッグログ
        AddSubTriggerIfNotExists(group, targetObj, "[PCD_Pipeline] Pipeline Controller & RenderPass & Kernel", TagPipeline, existingLabels);
        AddSubTriggerIfNotExists(group, targetObj, "[PCD_BufferManager] Point Buffer & Mesh Registrar", TagBuffer, existingLabels);

        // 2. Record（記録・エクスポート）デバッグログ
        AddSubTriggerIfNotExists(group, targetObj, "[PCD_RecordDebug] Record Debug Readback & Capture", TagRecordDebug, existingLabels);
        AddSubTriggerIfNotExists(group, targetObj, "[PCD_Exporter] Occlusion & Depth Map Exporter", TagExporter, existingLabels);
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
