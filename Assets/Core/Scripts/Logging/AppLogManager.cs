using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Core.Logging
{
    [Serializable]
    public class LogInstanceEntry
    {
        public string label;
        public string tag;
        public UnityEngine.Object target;

        public bool enableInfo = true;
        public bool enableWarning = true;
        public bool enableError = true;

        public bool IsEnabled(AppLogLevel level)
        {
            switch (level)
            {
                case AppLogLevel.Info: return enableInfo;
                case AppLogLevel.Warning: return enableWarning;
                case AppLogLevel.Error: return enableError;
                default: return true;
            }
        }

        public void SetAll(bool enable)
        {
            enableInfo = enable;
            enableWarning = enable;
            enableError = enable;
        }

        public bool enabled
        {
            get => enableInfo && enableWarning && enableError;
            set => SetAll(value);
        }
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
    [DefaultExecutionOrder(-1000)]
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

        private readonly object _lock = new object();
        private readonly Dictionary<UnityEngine.Object, LogInstanceEntry> _objectLookup = new Dictionary<UnityEngine.Object, LogInstanceEntry>();
        private readonly Dictionary<string, LogInstanceEntry> _nameLookup = new Dictionary<string, LogInstanceEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LogInstanceEntry> _targetTagLookup = new Dictionary<string, LogInstanceEntry>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            AppLogger.RegisterMainThread();
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            BuildLookup();
        }

        private void OnEnable()
        {
            AppLogger.RegisterMainThread();
            if (Instance == null) Instance = this;
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();
        }

        public void BuildLookup()
        {
            lock (_lock)
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

                        if (!ReferenceEquals(entry.target, null))
                        {
                            try
                            {
                                if (!_objectLookup.ContainsKey(entry.target))
                                {
                                    _objectLookup[entry.target] = entry;
                                }

                                if (!string.IsNullOrEmpty(entry.tag))
                                {
                                    string key = GetTargetTagKey(entry.target, entry.tag);
                                    _targetTagLookup[key] = entry;
                                }
                            }
                            catch (Exception) { }
                        }

                        if (!string.IsNullOrEmpty(entry.tag))
                        {
                            _nameLookup[entry.tag] = entry;
                        }

                        if (!string.IsNullOrEmpty(entry.label))
                        {
                            _nameLookup[entry.label] = entry;
                        }
                    }
                }
            }
        }

        private string GetTargetTagKey(UnityEngine.Object target, string tag)
        {
            if (ReferenceEquals(target, null)) return tag;
            try
            {
                return $"{target.GetInstanceID()}:{tag}";
            }
            catch (Exception)
            {
                return tag;
            }
        }

        /// <summary>
        /// コンポーネントインスタンスおよびオプションのサブタグ指定でログが有効かどうか判定
        /// </summary>
        public bool IsLogEnabled(UnityEngine.Object targetObject, string subTag = null)
        {
            return IsLogEnabled(targetObject, AppLogLevel.Info, subTag);
        }

        /// <summary>
        /// コンポーネントインスタンス、ログレベル、オプションのサブタグ指定でログが有効かどうか判定
        /// </summary>
        public bool IsLogEnabled(UnityEngine.Object targetObject, AppLogLevel level, string subTag = null)
        {
            if (!globalEnableLogging) return false;

            lock (_lock)
            {
                try
                {
                    // サブタグ指定がある場合、ターゲット+サブタグまたはサブタグ単体でのルックアップを優先
                    if (!string.IsNullOrEmpty(subTag))
                    {
                        if (!ReferenceEquals(targetObject, null))
                        {
                            string targetTagKey = GetTargetTagKey(targetObject, subTag);
                            if (_targetTagLookup.TryGetValue(targetTagKey, out var targetTagEntry))
                            {
                                return targetTagEntry.IsEnabled(level);
                            }
                        }

                        if (_nameLookup.TryGetValue(subTag, out var tagEntry))
                        {
                            return tagEntry.IsEnabled(level);
                        }
                    }

                    if (ReferenceEquals(targetObject, null)) return true;

                    if (_objectLookup.TryGetValue(targetObject, out var entry))
                    {
                        return entry.IsEnabled(level);
                    }
                }
                catch (Exception)
                {
                    return true;
                }

                return true; // 未登録コンポーネントはデフォルト表示 (ON)
            }
        }

        /// <summary>
        /// 指定されたタグが AppLogManager に登録されているか判定
        /// </summary>
        public bool IsTagRegistered(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            lock (_lock)
            {
                return _nameLookup.ContainsKey(tag);
            }
        }

        /// <summary>
        /// 名前/識別タグ指定でログが有効かどうか判定
        /// </summary>
        public bool IsLogEnabled(string nameTag)
        {
            return IsLogEnabled(nameTag, AppLogLevel.Info);
        }

        /// <summary>
        /// 名前/識別タグおよびログレベル指定でログが有効かどうか判定
        /// </summary>
        public bool IsLogEnabled(string nameTag, AppLogLevel level)
        {
            if (!globalEnableLogging) return false;

            if (string.IsNullOrEmpty(nameTag)) return true;

            lock (_lock)
            {
                try
                {
                    if (_nameLookup.TryGetValue(nameTag, out var entry))
                    {
                        return entry.IsEnabled(level);
                    }

                    foreach (var kvp in _nameLookup)
                    {
                        if (kvp.Key.IndexOf(nameTag, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return kvp.Value.IsEnabled(level);
                        }
                    }
                }
                catch (Exception)
                {
                    return true;
                }

                return true; // 未登録タグはデフォルト表示 (ON)
            }
        }

        /// <summary>
        /// シーン内のアクティブかつ AppLogger 対応コンポーネント（[AppLoggable] 属性または IAppLoggable 実装）のみを自動検出し登録
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
                    if (!string.IsNullOrEmpty(entry?.tag))
                    {
                        existingLabels.Add(entry.tag);
                    }
                }
            }

            // 非アクティブなオブジェクトおよび非アクティブなコンポーネントは除外 (FindObjectsInactive.Exclude)
            MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var comp in allComponents)
            {
                if (comp == null || comp == this) continue;
                if (!comp.gameObject.activeInHierarchy || !comp.enabled) continue;

                var loggableAttr = comp.GetType().GetCustomAttribute<AppLoggableAttribute>(true);
                bool isLoggableInterface = comp is IAppLoggable;

                // AppLogger を include / 対応していないコンポーネントは取得しない
                if (loggableAttr == null && !isLoggableInterface)
                {
                    continue;
                }

                string typeName = comp.GetType().Name;
                string catName = !string.IsNullOrEmpty(loggableAttr?.CategoryName)
                    ? loggableAttr.CategoryName
                    : (ResolveCategoryName(typeName) ?? "Other Loggables");

                LogCategoryGroup group = GetOrCreateGroup(catName);

                if (comp is IAppLoggable loggableComp)
                {
                    loggableComp.RegisterLogTriggers(group, existingLabels);
                    existingTargets.Add(comp);
                }
                else if (!existingTargets.Contains(comp))
                {
                    group.entries.Add(new LogInstanceEntry
                    {
                        label = $"[{typeName}] {comp.gameObject.name}",
                        tag = typeName,
                        target = comp,
                        enableInfo = true,
                        enableWarning = true,
                        enableError = true
                    });
                    existingTargets.Add(comp);
                }
            }

            // ScriptableObject / ScriptableRendererFeature の [AppLoggable] / IAppLoggable メモリ上全自動検出
            UnityEngine.Object[] allScriptableObjects = Resources.FindObjectsOfTypeAll(typeof(ScriptableObject));
            foreach (var obj in allScriptableObjects)
            {
                if (obj is ScriptableObject so && so != null)
                {
                    var loggableAttr = so.GetType().GetCustomAttribute<AppLoggableAttribute>(true);
                    bool isLoggableInterface = so is IAppLoggable;

                    if (loggableAttr == null && !isLoggableInterface)
                    {
                        continue;
                    }

                    string typeName = so.GetType().Name;
                    string catName = !string.IsNullOrEmpty(loggableAttr?.CategoryName)
                        ? loggableAttr.CategoryName
                        : (ResolveCategoryName(typeName) ?? "Other Loggables");

                    LogCategoryGroup group = GetOrCreateGroup(catName);

                    if (so is IAppLoggable loggableSo)
                    {
                        loggableSo.RegisterLogTriggers(group, existingLabels);
                        existingTargets.Add(so);
                    }
                    else if (!existingTargets.Contains(so))
                    {
                        group.entries.Add(new LogInstanceEntry
                        {
                            label = $"[{typeName}] {so.name}",
                            tag = typeName,
                            target = so,
                            enableInfo = true,
                            enableWarning = true,
                            enableError = true
                        });
                        existingTargets.Add(so);
                    }
                }
            }

            // 空のグループを削除
            categoryGroups.RemoveAll(g => g.entries == null || g.entries.Count == 0);

            BuildLookup();
        }

        private string ResolveCategoryName(string typeName)
        {
            if (typeName.StartsWith("HCD", StringComparison.OrdinalIgnoreCase)) return "HCD (Haptic Collision)";
            if (typeName.StartsWith("DPC", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Dummy")) return "DPC (Dummy Point Cloud)";
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

        public void SetAllEnabled(bool enable, AppLogLevel? targetLevel = null)
        {
            if (categoryGroups == null) return;
            foreach (var group in categoryGroups)
            {
                if (group?.entries == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry != null) SetEntryEnabled(entry, enable, targetLevel);
                }
            }
            BuildLookup();
        }

        public void SetGroupEnabled(string categoryName, bool enable, AppLogLevel? targetLevel = null)
        {
            var group = categoryGroups?.Find(g => string.Equals(g.categoryName, categoryName, StringComparison.OrdinalIgnoreCase));
            if (group?.entries != null)
            {
                foreach (var entry in group.entries)
                {
                    if (entry != null) SetEntryEnabled(entry, enable, targetLevel);
                }
                BuildLookup();
            }
        }

        private void SetEntryEnabled(LogInstanceEntry entry, bool enable, AppLogLevel? targetLevel)
        {
            if (targetLevel.HasValue)
            {
                switch (targetLevel.Value)
                {
                    case AppLogLevel.Info: entry.enableInfo = enable; break;
                    case AppLogLevel.Warning: entry.enableWarning = enable; break;
                    case AppLogLevel.Error: entry.enableError = enable; break;
                }
            }
            else
            {
                entry.SetAll(enable);
            }
        }
    }
}

