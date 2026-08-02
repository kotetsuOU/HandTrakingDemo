using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// DummyPointCloud (DPC) モジュールの AppLogManager 連動ログトリガー定義および登録を担当するコンポーネント。
    /// </summary>
    [AppLoggable("DummyPointCloud")]
    [DisallowMultipleComponent]
    public class DPC_LogTriggers : MonoBehaviour, IAppLoggable
    {
        public const string TagProvider = "DPC_Provider";
        public const string TagRenderer = "DPC_Renderer";
        public const string TagNoiseProcessor = "DPC_NoiseProcessor";

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var provider = GetComponent<RsDummyPointCloudProvider>() ?? FindFirstObjectByType<RsDummyPointCloudProvider>();
            Object providerObj = provider != null ? (Object)provider : this;

            var renderer = GetComponent<RsDummyPointCloudRenderer>() ?? FindFirstObjectByType<RsDummyPointCloudRenderer>();
            Object rendererObj = renderer != null ? (Object)renderer : this;

            AddSubTriggerIfNotExists(group, providerObj, "[DPC_Provider] Dummy Point Cloud Sampler & Streaming Provider", TagProvider, existingLabels);
            AddSubTriggerIfNotExists(group, rendererObj, "[DPC_Renderer] Dirty-based GPU Point Cloud Renderer", TagRenderer, existingLabels);
            AddSubTriggerIfNotExists(group, providerObj, "[DPC_NoiseProcessor] Normal-Direction Noise & Outliers Processor", TagNoiseProcessor, existingLabels);
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
