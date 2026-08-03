using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

/// <summary>
/// PointCloudViewer (PCV) モジュールの AppLogManager 連動ログトリガー定義および登録を担当するコンポーネント。
/// </summary>
[AppLoggable("PCV (PointCloudViewer)")]
[DisallowMultipleComponent]
public class PCV_LogTriggers : MonoBehaviour, IAppLoggable
{
    public const string TagController = "PCV_Controller";
    public const string TagDataManager = "PCV_DataManager";
    public const string TagLoader = "PCV_Loader";
    public const string TagRenderer = "PCV_Renderer";
    public const string TagConfigIO = "PCV_ConfigIO";

    public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
    {
        var controller = GetComponent<PCV_Controller>() ?? FindFirstObjectByType<PCV_Controller>();
        Object controllerObj = controller != null ? (Object)controller : this;

        var dataManager = GetComponent<PCV_DataManager>() ?? FindFirstObjectByType<PCV_DataManager>();
        Object dataManagerObj = dataManager != null ? (Object)dataManager : this;

        var renderer = GetComponent<PCV_Renderer>() ?? FindFirstObjectByType<PCV_Renderer>();
        Object rendererObj = renderer != null ? (Object)renderer : this;

        AddSubTriggerIfNotExists(group, controllerObj, "[PCV_Controller] PointCloudViewer Controller & Calibration", TagController, existingLabels);
        AddSubTriggerIfNotExists(group, dataManagerObj, "[PCV_DataManager] PointCloud Data Manager", TagDataManager, existingLabels);
        AddSubTriggerIfNotExists(group, controllerObj, "[PCV_Loader] PointCloud File Loader (PLY / CSV)", TagLoader, existingLabels);
        AddSubTriggerIfNotExists(group, rendererObj, "[PCV_Renderer] PointCloud Mesh Renderer", TagRenderer, existingLabels);
        AddSubTriggerIfNotExists(group, controllerObj, "[PCV_ConfigIO] Profile IO (Save / Load Config)", TagConfigIO, existingLabels);
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
