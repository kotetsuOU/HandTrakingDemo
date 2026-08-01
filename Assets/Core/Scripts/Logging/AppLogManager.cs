using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Logging
{
    [Serializable]
    public class LogInstanceEntry
    {
        public string label;
        public string tag;
        public UnityEngine.Object target;
        public bool enabled;
    }

    [Serializable]
    public class LogCategoryGroup
    {
        public string categoryName;
        public bool isExpanded = true;
        public List<LogInstanceEntry> entries = new List<LogInstanceEntry>();
    }

    /// <summary>
    /// シーン内の各コンポーネントインスタンスおよび個別ログトリガーのON/OFFを
    /// モジュール別（HCD, RealSense, PCD, Experiment等）のグループ階層で一元集中管理する MonoBehaviour マネージャー。
    /// </summary>
    [DisallowMultipleComponent]
    public class AppLogManager : MonoBehaviour
    {
        public static AppLogManager Instance { get; private set; }

        [Header("Global Control")]
        [Tooltip("全体的なログ出力の有効/無効トグル")]
        public bool globalEnableLogging = true;

        [Header("Category Groups")]
        [Tooltip("モジュール機能ごとにグループ化されたコンポーネントターゲット")]
        public List<LogCategoryGroup> categoryGroups = new List<LogCategoryGroup>();

        private readonly Dictionary<UnityEngine.Object, LogInstanceEntry> _objectLookup = new Dictionary<UnityEngine.Object, LogInstanceEntry>();
        private readonly Dictionary<string, bool> _nameLookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _targetTagLookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildLookup();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();
        }

        public void BuildLookup()
        {
            _objectLookup.Clear();
            _nameLookup.Clear();
            _targetTagLookup.Clear();

            if (categoryGroups == null) return;

            foreach (var group in categoryGroups)
            {
                if (group == null || group.entries == null) continue;

                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;

                    if (entry.target != null)
                    {
                        if (!_objectLookup.ContainsKey(entry.target))
                        {
                            _objectLookup[entry.target] = entry;
                        }

                        if (!string.IsNullOrEmpty(entry.tag))
                        {
                            string key = GetTargetTagKey(entry.target, entry.tag);
                            _targetTagLookup[key] = entry.enabled;
                        }
                    }

                    if (!string.IsNullOrEmpty(entry.tag))
                    {
                        _nameLookup[entry.tag] = entry.enabled;
                    }

                    if (!string.IsNullOrEmpty(entry.label))
                    {
                        _nameLookup[entry.label] = entry.enabled;
                    }
                }
            }
        }

        private string GetTargetTagKey(UnityEngine.Object target, string tag)
        {
            return target != null ? $"{target.GetInstanceID()}:{tag}" : tag;
        }

        /// <summary>
        /// コンポーネントインスタンスおよびオプションのサブタグ指定でログが有効かどうか判定
        /// </summary>
        public bool IsLogEnabled(UnityEngine.Object targetObject, string subTag = null)
        {
            if (!globalEnableLogging) return false;

            // サブタグ指定がある場合、ターゲット+サブタグまたはサブタグ単体でのルックアップを優先
            if (!string.IsNullOrEmpty(subTag))
            {
                if (targetObject != null)
                {
                    string targetTagKey = GetTargetTagKey(targetObject, subTag);
                    if (_targetTagLookup.TryGetValue(targetTagKey, out bool targetTagEnabled))
                    {
                        return targetTagEnabled;
                    }
                }

                if (_nameLookup.TryGetValue(subTag, out bool tagEnabled))
                {
                    return tagEnabled;
                }
            }

            if (targetObject == null) return true;

            if (_objectLookup.TryGetValue(targetObject, out var entry))
            {
                return entry.enabled;
            }

            return false; // 未登録の場合は非表示
        }

        /// <summary>
        /// 名前/識別タグ指定でログが有効かどうか判定
        /// </summary>
        public bool IsLogEnabled(string nameTag)
        {
            if (!globalEnableLogging) return false;
            if (string.IsNullOrEmpty(nameTag)) return true;

            if (_nameLookup.TryGetValue(nameTag, out bool enabled))
            {
                return enabled;
            }

            return false;
        }

        /// <summary>
        /// シーン内のログ出力可能コンポーネントを自動検出し、カテゴリ別にグループ分けして登録
        /// </summary>
        public void ScanSceneComponents()
        {
            if (categoryGroups == null) categoryGroups = new List<LogCategoryGroup>();

            HashSet<UnityEngine.Object> existingTargets = new HashSet<UnityEngine.Object>();
            HashSet<string> existingLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in categoryGroups)
            {
                if (group?.entries == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry?.target != null)
                    {
                        existingTargets.Add(entry.target);
                    }
                    if (!string.IsNullOrEmpty(entry?.label))
                    {
                        existingLabels.Add(entry.label);
                    }
                }
            }

            MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var comp in allComponents)
            {
                if (comp == null || comp == this) continue;

                string typeName = comp.GetType().Name;
                string catName = ResolveCategoryName(typeName);

                if (catName != null)
                {
                    LogCategoryGroup group = GetOrCreateGroup(catName);

                    // HCD_Pipeline 特有のサブトリガー自動登録
                    if (comp.GetType().Name == "HCD_Pipeline")
                    {
                        AddSubTriggerIfNotExists(group, comp, "[HCD_Pipeline] Summary & Readback", "HCD_Pipeline", existingLabels);
                        AddSubTriggerIfNotExists(group, comp, "[HCD_DistanceProcessor] Mesh & Bounds Debug", "HCD_DistanceProcessor", existingLabels);
                        AddSubTriggerIfNotExists(group, comp, "[HCD_SpatialClusteringProcessor] Clustering Debug", "HCD_SpatialClusteringProcessor", existingLabels);
                        AddSubTriggerIfNotExists(group, comp, "[HCD_ClusterTracker] Cluster Tracking Info", "HCD_ClusterTracker", existingLabels);
                        existingTargets.Add(comp);
                    }
                    else if (!existingTargets.Contains(comp))
                    {
                        group.entries.Add(new LogInstanceEntry
                        {
                            label = $"[{typeName}] {comp.gameObject.name}",
                            tag = typeName,
                            target = comp,
                            enabled = false
                        });
                        existingTargets.Add(comp);
                    }
                }
            }

            // 空のグループを削除
            categoryGroups.RemoveAll(g => g.entries == null || g.entries.Count == 0);

            BuildLookup();
        }

        private void AddSubTriggerIfNotExists(LogCategoryGroup group, MonoBehaviour comp, string label, string tag, HashSet<string> existingLabels)
        {
            if (!existingLabels.Contains(label))
            {
                group.entries.Add(new LogInstanceEntry
                {
                    label = label,
                    tag = tag,
                    target = comp,
                    enabled = false
                });
                existingLabels.Add(label);
            }
        }

        private string ResolveCategoryName(string typeName)
        {
            if (typeName.StartsWith("HCD", StringComparison.OrdinalIgnoreCase)) return "HCD (Haptic Collision)";
            if (typeName.StartsWith("Rs", StringComparison.OrdinalIgnoreCase) || typeName.Contains("RealSense")) return "RealSense";
            if (typeName.StartsWith("PCD", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Occlusion")) return "PCD (Occlusion)";
            if (typeName.StartsWith("EXP", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Experiment")) return "Experiment";
            if (typeName.StartsWith("HAP", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Haptic")) return "Haptics";
            if (typeName.Contains("Controller") || typeName.Contains("Manager")) return "Core / Utilities";
            return null;
        }

        private LogCategoryGroup GetOrCreateGroup(string categoryName)
        {
            var group = categoryGroups.Find(g => string.Equals(g.categoryName, categoryName, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new LogCategoryGroup { categoryName = categoryName, isExpanded = true };
                categoryGroups.Add(group);
            }
            return group;
        }

        public void SetAllEnabled(bool enable)
        {
            if (categoryGroups == null) return;
            foreach (var group in categoryGroups)
            {
                if (group?.entries == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry != null) entry.enabled = enable;
                }
            }
            BuildLookup();
        }

        public void SetGroupEnabled(string categoryName, bool enable)
        {
            var group = categoryGroups?.Find(g => string.Equals(g.categoryName, categoryName, StringComparison.OrdinalIgnoreCase));
            if (group?.entries != null)
            {
                foreach (var entry in group.entries)
                {
                    if (entry != null) entry.enabled = enable;
                }
                BuildLookup();
            }
        }
    }
}

